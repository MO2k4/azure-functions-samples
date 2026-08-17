using Dapr.Client;

namespace DaprDemo.Orders;

/// <summary>
/// One order flow across the three Dapr building blocks this sample covers: state management,
/// service invocation, and publish/subscribe. Every Dapr resource is addressed by component
/// name or app ID, never by host, port, or connection string, which is what lets the same code
/// run against components/local and components/azure unchanged.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// Dapr app ID of the shipping service. Service invocation resolves this; there is no
    /// hostname or port anywhere in the calling code.
    /// </summary>
    public const string ShippingAppId = "shipping-api";

    /// <summary>
    /// Component <c>metadata.name</c> of the state store. Identical in the Redis and the
    /// Cosmos DB component files, which is the point.
    /// </summary>
    private const string StateStoreName = "orderstore";

    /// <summary>Component <c>metadata.name</c> of the pub/sub broker (Redis or Service Bus).</summary>
    private const string PubSubName = "orderpubsub";

    private const string OrderCreatedTopic = "orders.created";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", PlaceOrderAsync);
        app.MapGet("/orders/{orderId}", GetOrderAsync);
        app.MapDelete("/orders/{orderId}", CancelOrderAsync);

        // The service invocation target. orders-api reaches it as POST http://shipping-api/shipments.
        app.MapPost("/shipments", QuoteShipment);

        // The pub/sub subscriber. WithTopic adds the metadata that MapSubscribeHandler reports
        // to the sidecar on GET /dapr/subscribe; the route name itself is arbitrary.
        app.MapPost("/events/order-created", OnOrderCreatedAsync)
            .WithTopic(PubSubName, OrderCreatedTopic);
    }

    /// <summary>
    /// Saves the order (state management), asks the shipping service for a quote (service
    /// invocation), then announces the order (pub/sub).
    /// </summary>
    private static async Task<IResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        DaprClient dapr,
        [FromKeyedServices(ShippingAppId)] HttpClient shipping,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
        {
            return Results.BadRequest("An order needs at least one line.");
        }

        var order = new Order(
            request.OrderId,
            request.CustomerId,
            request.Lines,
            request.Lines.Sum(line => line.UnitPrice * line.Quantity),
            OrderStatus.Placed);

        // State management, save. Dapr prefixes the physical key with the app ID and a "||"
        // separator by default, so orders-api storing "ORD-1001" writes "orders-api||ORD-1001"
        // into Redis or Cosmos DB. Change that with the component's keyPrefix metadata field.
        await dapr.SaveStateAsync(StateStoreName, order.OrderId, order, cancellationToken: cancellationToken);

        // Service invocation. An ordinary POST: the BaseAddress is http://shipping-api, and the
        // sidecar handles discovery, mTLS, retries, load balancing across instances, and trace
        // propagation. Dapr does not replace the HttpClient; it replaces everything behind it.
        var shipmentRequest = new ShipmentRequest(
            order.OrderId,
            order.CustomerId,
            order.Lines.Sum(line => line.Quantity));

        using var response = await shipping.PostAsJsonAsync("/shipments", shipmentRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        var quote = await response.Content.ReadFromJsonAsync<ShipmentQuote>(cancellationToken);

        // Pub/sub, publish side. Delivery is at-least-once, so the subscriber below has to be
        // idempotent. The payload is wrapped in a CloudEvent envelope on the way out.
        var orderCreated = new OrderCreated(
            order.OrderId,
            order.CustomerId,
            order.Total,
            quote?.Carrier ?? "unassigned");

        await dapr.PublishEventAsync(PubSubName, OrderCreatedTopic, orderCreated, cancellationToken);

        logger.LogInformation("Placed order {OrderId} totalling {Total}.", order.OrderId, order.Total);

        return Results.Accepted(
            $"/orders/{order.OrderId}",
            new PlaceOrderResponse(order.OrderId, order.Total, quote));
    }

    /// <summary>State management, get. A missing key comes back as the default value, not an error.</summary>
    private static async Task<IResult> GetOrderAsync(
        string orderId,
        DaprClient dapr,
        CancellationToken cancellationToken)
    {
        var order = await dapr.GetStateAsync<Order>(StateStoreName, orderId, cancellationToken: cancellationToken);

        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    /// <summary>State management, delete. Deleting a key that was never there is not an error either.</summary>
    private static async Task<IResult> CancelOrderAsync(
        string orderId,
        DaprClient dapr,
        CancellationToken cancellationToken)
    {
        await dapr.DeleteStateAsync(StateStoreName, orderId, cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// The shipping service. Nothing in here is Dapr-aware: it is a plain minimal API handler
    /// that happens to be reachable through the sidecar under the app ID "shipping-api".
    /// </summary>
    private static IResult QuoteShipment(ShipmentRequest request)
    {
        var carrier = request.ItemCount > 10 ? "freight-forwarder" : "parcel-express";
        var transitDays = carrier == "freight-forwarder" ? 5 : 2;

        return Results.Ok(new ShipmentQuote(
            request.OrderId,
            carrier,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(transitDays)));
    }

    /// <summary>
    /// Pub/sub, subscriber side. The status in the response body tells Dapr what to do next:
    /// SUCCESS acknowledges, RETRY redelivers, DROP discards. A 404 also drops the message and
    /// any other non-2xx retries it.
    /// </summary>
    private static async Task<IResult> OnOrderCreatedAsync(
        OrderCreated orderCreated,
        DaprClient dapr,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        var order = await dapr.GetStateAsync<Order>(
            StateStoreName, orderCreated.OrderId, cancellationToken: cancellationToken);

        if (order is null)
        {
            logger.LogWarning("No stored order for {OrderId}; dropping the event.", orderCreated.OrderId);

            return Results.NotFound();
        }

        // Idempotent by construction: re-delivering the event re-writes the same status.
        await dapr.SaveStateAsync(
            StateStoreName,
            order.OrderId,
            order with { Status = OrderStatus.Confirmed },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Confirmed order {OrderId}, shipping with {Carrier}.", orderCreated.OrderId, orderCreated.Carrier);

        return Results.Ok(new SubscriptionResponse("SUCCESS"));
    }
}

/// <summary>A single line on an order.</summary>
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>Request body for <c>POST /orders</c>.</summary>
public sealed record PlaceOrderRequest(string OrderId, string CustomerId, IReadOnlyList<OrderLine> Lines);

/// <summary>The order as it is stored in the Dapr state store.</summary>
public sealed record Order(
    string OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    OrderStatus Status);

/// <summary>Lifecycle of an order in this sample.</summary>
public enum OrderStatus
{
    /// <summary>Written to the state store, not yet acknowledged by the subscriber.</summary>
    Placed,

    /// <summary>The order-created event was handled.</summary>
    Confirmed,

    /// <summary>Removed from the state store.</summary>
    Cancelled,
}

/// <summary>Request body sent to the shipping service over service invocation.</summary>
public sealed record ShipmentRequest(string OrderId, string CustomerId, int ItemCount);

/// <summary>What the shipping service answers with.</summary>
public sealed record ShipmentQuote(string OrderId, string Carrier, DateOnly EstimatedDelivery);

/// <summary>The event published to the <c>orders.created</c> topic.</summary>
public sealed record OrderCreated(string OrderId, string CustomerId, decimal Total, string Carrier);

/// <summary>Response body for <c>POST /orders</c>.</summary>
public sealed record PlaceOrderResponse(string OrderId, decimal Total, ShipmentQuote? Shipment);

/// <summary>Body Dapr reads to decide whether a delivered message is done, retried, or dropped.</summary>
public sealed record SubscriptionResponse(string Status);

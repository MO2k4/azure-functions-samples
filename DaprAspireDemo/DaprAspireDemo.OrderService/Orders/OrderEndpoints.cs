using Dapr.Client;
using DaprAspireDemo.OrderService.Inventory;

namespace DaprAspireDemo.OrderService.Orders;

/// <summary>
/// The order routes. Two Dapr building blocks are in play and neither one names a host, a port or
/// a credential: service invocation addresses <c>inventory-service</c> by app ID, state management
/// addresses <c>statestore</c> by component name.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>Maps the order routes.</summary>
    /// <param name="app">The application's route builder.</param>
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var orders = app.MapGroup("/orders");

        orders.MapPost("/", CreateOrderAsync).WithName("CreateOrder");
        orders.MapGet("/{orderId}", GetOrderAsync).WithName("GetOrder");
    }

    /// <summary>
    /// Asks inventory-service whether the lines can be fulfilled, then writes the order to the
    /// state store. This is the request that produces the trace the article is about: one span
    /// for this handler, one for the outbound HttpClient call, one in each sidecar, one for the
    /// handler on the far side.
    /// </summary>
    private static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest request,
        InventoryClient inventory,
        DaprClient dapr,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("An order needs at least one line."));
        }

        var stockRequest = new StockCheckRequest(
            request.OrderId,
            [.. request.Lines.Select(line => new StockLine(line.Sku, line.Quantity))]);

        // Service invocation. The failure is a value, so both interesting branches (inventory said
        // no; inventory could not be asked) are visible in the same switch.
        var stock = await inventory.CheckStockAsync(stockRequest, cancellationToken);

        switch (stock)
        {
            case Result<StockCheckResponse>.Failure(var error):
                return ToProblem(error);

            case Result<StockCheckResponse>.Success({ Available: false } answer):
                logger.LogInformation(
                    "Rejected order {OrderId}: inventory short on {Count} line(s).",
                    request.OrderId,
                    answer.Shortfalls.Count);

                return Results.Conflict(new OutOfStockResponse(request.OrderId, answer.Shortfalls));
        }

        var order = new Order(
            request.OrderId,
            request.CustomerId,
            request.Lines,
            request.Lines.Sum(line => line.UnitPrice * line.Quantity),
            OrderStatus.Placed,
            DateTimeOffset.UtcNow);

        // State management. Whether this write survives an app restart is a property of the
        // component the sidecar loaded, not of this line: the same call is Redis in one
        // configuration and an in-memory store in another.
        await dapr.SaveStateAsync(
            StateStore.Name,
            order.OrderId,
            order,
            cancellationToken: cancellationToken);

        logger.LogInformation("Placed order {OrderId} totalling {Total}.", order.OrderId, order.Total);

        return Results.Created($"/orders/{order.OrderId}", order);
    }

    /// <summary>
    /// Reads an order back. This is the half of the persistence experiment that matters: restart
    /// the app resource, call this, and the answer tells you which component actually loaded.
    /// </summary>
    private static async Task<IResult> GetOrderAsync(
        string orderId,
        DaprClient dapr,
        CancellationToken cancellationToken)
    {
        var order = await dapr.GetStateAsync<Order>(
            StateStore.Name,
            orderId,
            cancellationToken: cancellationToken);

        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    /// <summary>
    /// Maps an invocation failure onto a status the caller can act on. A target the sidecar cannot
    /// route to is 503, because that is usually a startup race and is worth retrying; an error the
    /// target itself produced is 502, because retrying will not help.
    /// </summary>
    private static IResult ToProblem(InvocationError error) => error.Kind switch
    {
        InvocationFailure.TargetUnreachable or InvocationFailure.SidecarUnreachable =>
            Results.Problem(
                title: "Inventory is unavailable.",
                detail: error.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable),

        InvocationFailure.Timeout =>
            Results.Problem(
                title: "Inventory did not answer in time.",
                detail: error.Message,
                statusCode: StatusCodes.Status504GatewayTimeout),

        _ => Results.Problem(
            title: "Inventory rejected the stock check.",
            detail: error.Message,
            statusCode: StatusCodes.Status502BadGateway),
    };
}

/// <summary>Request body for <c>POST /orders</c>.</summary>
public sealed record CreateOrderRequest(string OrderId, string CustomerId, IReadOnlyList<OrderLine> Lines);

/// <summary>Body returned when inventory cannot fulfil the order.</summary>
public sealed record OutOfStockResponse(string OrderId, IReadOnlyList<StockShortfall> Shortfalls);

/// <summary>A plain message body for the validation failures that are not worth a ProblemDetails.</summary>
public sealed record ErrorResponse(string Message);

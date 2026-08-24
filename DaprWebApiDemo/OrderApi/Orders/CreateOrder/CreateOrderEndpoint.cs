using Dapr.Client;
using OrderApi.Inventory;

namespace OrderApi.Orders.CreateOrder;

/// <summary>
/// The order-creation slice: route, contracts, and handler in one file. Everything this
/// feature needs is here, and nothing else needs to change to add the next feature.
/// </summary>
public static class CreateOrderEndpoint
{
    /// <summary>Registers <c>POST /orders</c> on the supplied group.</summary>
    /// <param name="orders">The <c>/orders</c> group, already carrying the token filter.</param>
    public static void MapCreateOrder(this IEndpointRouteBuilder orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        orders.MapPost("/", CreateOrderAsync)
            .WithName("CreateOrder");
    }

    /// <summary>
    /// Asks inventory-api whether the lines can be fulfilled, then writes the order to the
    /// state store. Two Dapr building blocks, neither of which names a host or a credential.
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

        // Service invocation. The failure is a value, so both interesting branches (inventory
        // said no; inventory could not be asked) are visible in the same switch.
        var stock = await inventory.CheckStockAsync(stockRequest, cancellationToken);

        switch (stock)
        {
            case Result<StockCheckResponse>.Failure(var error):
                return ToProblem(error);

            case Result<StockCheckResponse>.Success({ Available: false } answer):
                logger.LogInformation("Rejected order {OrderId}: inventory short on {Count} line(s).",
                    request.OrderId, answer.Shortfalls.Count);

                return Results.Conflict(new OutOfStockResponse(request.OrderId, answer.Shortfalls));
        }

        var order = new Order(
            request.OrderId,
            request.CustomerId,
            request.Lines,
            request.Lines.Sum(line => line.UnitPrice * line.Quantity),
            OrderStatus.Placed,
            DateTimeOffset.UtcNow,
            ConfirmedAt: null);

        // State management. SaveStateAsync has no ETag parameter at all: an unconditional
        // write is last-write-wins by construction. That is the right call for a create, and
        // the wrong call for the confirm in the sibling slice.
        //
        // The metadata is the Cosmos DB partition, and it is on all four slices or none of
        // them. OrderPartition says why; this is the write that decides where the document
        // lands, so it is the one to get right first.
        await dapr.SaveStateAsync(
            StateStore.Name,
            order.OrderId,
            order,
            metadata: OrderPartition.For(order.CustomerId),
            cancellationToken: cancellationToken);

        logger.LogInformation("Placed order {OrderId} totalling {Total}.", order.OrderId, order.Total);

        // The customer ID is in the location for the same reason it is in the metadata: a reader
        // cannot address the order without naming its partition.
        return Results.Created($"/orders/{order.CustomerId}/{order.OrderId}", order);
    }

    /// <summary>
    /// Maps an invocation failure onto a status the caller can act on. A target the sidecar
    /// cannot route to is 503, because that is usually a startup race and is worth retrying;
    /// an error the target itself produced is 502, because retrying will not help.
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

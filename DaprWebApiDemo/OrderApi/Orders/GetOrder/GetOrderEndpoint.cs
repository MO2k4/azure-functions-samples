using Dapr.Client;

namespace OrderApi.Orders.GetOrder;

/// <summary>Reads one order back out of the state store.</summary>
public static class GetOrderEndpoint
{
    /// <summary>Registers <c>GET /orders/{customerId}/{orderId}</c> on the supplied group.</summary>
    /// <param name="orders">The <c>/orders</c> group, already carrying the token filter.</param>
    public static void MapGetOrder(this IEndpointRouteBuilder orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        orders.MapGet("/{customerId}/{orderId}", GetOrderAsync)
            .WithName("GetOrder");
    }

    /// <summary>
    /// A missing key is not an error in Dapr: <c>GetStateAsync</c> returns the default value,
    /// so "no such order" and "the store is empty" look identical from here. Turning that into
    /// a 404 is the app's decision, not the runtime's.
    /// </summary>
    /// <remarks>
    /// The customer ID is a route segment because the partition value has to be known before the
    /// read, and the only other place it lives is inside the document the read is trying to
    /// fetch. See <see cref="OrderPartition"/>.
    /// </remarks>
    private static async Task<IResult> GetOrderAsync(
        string customerId,
        string orderId,
        DaprClient dapr,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dapr);

        var order = await dapr.GetStateAsync<Order>(
            StateStore.Name,
            orderId,
            metadata: OrderPartition.For(customerId),
            cancellationToken: cancellationToken);

        // Cosmos DB enforces the second half of this on its own: an order from another customer
        // is in another partition and comes back null. Redis ignores the metadata and hands over
        // whatever sits under the key, so the check is what keeps both stores answering alike.
        return order is null || !string.Equals(order.CustomerId, customerId, StringComparison.Ordinal)
            ? Results.NotFound()
            : Results.Ok(order);
    }
}

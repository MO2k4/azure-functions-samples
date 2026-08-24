using System.Text.Json;
using Dapr.Client;
using OrderApi.Orders.CreateOrder;

namespace OrderApi.Orders.CancelOrder;

/// <summary>
/// The transaction slice. Cancelling an order changes the order and records why, and the two
/// writes are only worth anything together.
/// </summary>
public static class CancelOrderEndpoint
{
    /// <summary>Registers <c>POST /orders/{customerId}/{orderId}/cancel</c> on the supplied group.</summary>
    /// <param name="orders">The <c>/orders</c> group, already carrying the token filter.</param>
    public static void MapCancelOrder(this IEndpointRouteBuilder orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        orders.MapPost("/{customerId}/{orderId}/cancel", CancelOrderAsync)
            .WithName("CancelOrder");
    }

    /// <summary>
    /// Writes the cancelled order and its audit record as one atomic set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bulk would not do.</b> <c>SaveBulkStateAsync</c> over the same two items is N
    /// independent writes travelling together, so it can leave the order cancelled with no
    /// record of why, or a record of a cancellation that never happened.
    /// <c>ExecuteStateTransactionAsync</c> is the atomic one, and only against a store that
    /// declares transaction support: Cosmos DB and Redis do, Blob Storage and Table Storage
    /// reject the call outright.
    /// </para>
    /// <para>
    /// <b>The value is <c>byte[]</c>, not <c>TValue</c>.</b> Every other state method on
    /// <c>DaprClient</c> is generic and serialises for you with the options the client was
    /// configured with. <c>StateTransactionRequest</c> takes bytes, so the serialisation moves
    /// into the handler, and it has to produce the same JSON the client would have produced or
    /// <c>GetStateAsync&lt;Order&gt;</c> in the sibling slice cannot read the value back.
    /// Going through <c>OrderApiJsonContext</c> here is what keeps the two ends in step.
    /// </para>
    /// <para>
    /// <b>This is the slice that forced the partition decision.</b> Cosmos DB will not commit a
    /// transaction whose items span partitions, and for non-actor state the default partition
    /// key value is the item's own state key, so the order and its audit record are on two
    /// partitions until something says otherwise. Nothing in the .NET signature hints at it:
    /// <c>metadata</c> is an opaque string bag forwarded to the component verbatim, and Redis
    /// drops the entry. <see cref="OrderPartition"/> carries the reasoning, and why the other
    /// three slices pass the same value rather than leaving this one to override it alone.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CancelOrderAsync(
        string customerId,
        string orderId,
        CancelOrderRequest request,
        DaprClient dapr,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dapr);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new ErrorResponse("A cancellation needs a reason."));
        }

        // One partition for the order, its audit record, and every operation below.
        var partition = OrderPartition.For(customerId);

        var order = await dapr.GetStateAsync<Order>(
            StateStore.Name,
            orderId,
            metadata: partition,
            cancellationToken: cancellationToken);

        // Same as everywhere else in this sample: a missing key is a default value, not an
        // error, so the null check comes before anything reads the order, and the customer
        // check beside it makes Redis answer the way Cosmos DB already would.
        if (order is null || !string.Equals(order.CustomerId, customerId, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        if (order.Status is OrderStatus.Cancelled)
        {
            // Already done. The audit record is in the same partition as the order, which is
            // the whole reason the transaction below could write them together.
            var recorded = await dapr.GetStateAsync<OrderCancellation>(
                StateStore.Name,
                CancellationKey(orderId),
                metadata: partition,
                cancellationToken: cancellationToken);

            return Results.Ok(new CancelOrderResponse(order, recorded));
        }

        if (order.Status is OrderStatus.Confirmed)
        {
            // A confirmed order has already committed stock. Unwinding that is a workflow, not
            // a state-store write, so this endpoint refuses rather than pretending.
            return Results.Conflict(new CancelConflictResponse(orderId, order.Status));
        }

        // No ETag on either operation, deliberately. There is no read inside a transaction, so
        // the order above was read before the set was built and this is last-write-wins on the
        // order key. StateTransactionRequest does take an etag, and a cancel that has to lose
        // to a concurrent confirm would read with GetStateAndETagAsync and pass it here: the
        // call returns a bare Task with no bool in it, so the conflict can only arrive as an
        // exception, and the retry loop in the sibling slice has to be written differently.
        var cancelled = order with { Status = OrderStatus.Cancelled };

        var cancellation = new OrderCancellation(
            order.OrderId, order.CustomerId, request.Reason, order.Status, DateTimeOffset.UtcNow);

        // The serialisation the transactional API does not do for you.
        var orderBytes = JsonSerializer.SerializeToUtf8Bytes(cancelled, OrderApiJsonContext.Default.Order);
        var auditBytes = JsonSerializer.SerializeToUtf8Bytes(cancellation, OrderApiJsonContext.Default.OrderCancellation);

        await dapr.ExecuteStateTransactionAsync(
            StateStore.Name,
            [
                new StateTransactionRequest(order.OrderId, orderBytes, StateOperationType.Upsert, metadata: partition),
                new StateTransactionRequest(CancellationKey(order.OrderId), auditBytes, StateOperationType.Upsert, metadata: partition),
            ],
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Cancelled order {OrderId} for customer {CustomerId}.", order.OrderId, order.CustomerId);

        return Results.Ok(new CancelOrderResponse(cancelled, cancellation));
    }

    /// <summary>
    /// Key of the audit record beside the order. A second key, deliberately: the point of the
    /// transaction is that it writes two of them.
    /// </summary>
    /// <param name="orderId">The order being cancelled.</param>
    private static string CancellationKey(string orderId) => $"{orderId}-cancellation";
}

/// <summary>Request body for <c>POST /orders/{orderId}/cancel</c>.</summary>
/// <param name="Reason">Why the order is being cancelled; recorded in the audit key.</param>
public sealed record CancelOrderRequest(string Reason);

/// <summary>
/// The audit record written alongside the order. Stored under its own state key, which is what
/// makes the write a transaction rather than a save.
/// </summary>
/// <param name="OrderId">The order this record belongs to.</param>
/// <param name="CustomerId">The Cosmos DB partition both keys in the set were written to.</param>
/// <param name="Reason">The reason supplied by the caller.</param>
/// <param name="PreviousStatus">What the order was before the cancellation.</param>
/// <param name="CancelledAt">When the transaction was built.</param>
public sealed record OrderCancellation(
    string OrderId,
    string CustomerId,
    string Reason,
    OrderStatus PreviousStatus,
    DateTimeOffset CancelledAt);

/// <summary>Both halves of the transaction, so the caller can see what committed together.</summary>
/// <param name="Order">The order in its cancelled state.</param>
/// <param name="Cancellation">The audit record, or <c>null</c> if the key could not be read back.</param>
public sealed record CancelOrderResponse(Order Order, OrderCancellation? Cancellation);

/// <summary>Body returned when the order is past the point where cancelling is a write.</summary>
/// <param name="OrderId">The order that was not cancelled.</param>
/// <param name="Status">The status that blocked it.</param>
public sealed record CancelConflictResponse(string OrderId, OrderStatus Status);

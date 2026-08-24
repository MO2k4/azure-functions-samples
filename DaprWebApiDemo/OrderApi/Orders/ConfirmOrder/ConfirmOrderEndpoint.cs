using Dapr.Client;

namespace OrderApi.Orders.ConfirmOrder;

/// <summary>
/// The optimistic-concurrency slice. Confirming an order is a read-modify-write, which is the
/// one shape a key-value store cannot make safe on its own.
/// </summary>
public static class ConfirmOrderEndpoint
{
    /// <summary>
    /// How many times a confirm will re-read and try again before giving up. Bounded on
    /// purpose: an unbounded loop under real contention is a livelock with good manners.
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>Registers <c>POST /orders/{customerId}/{orderId}/confirm</c> on the supplied group.</summary>
    /// <param name="orders">The <c>/orders</c> group, already carrying the token filter.</param>
    public static void MapConfirmOrder(this IEndpointRouteBuilder orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        orders.MapPost("/{customerId}/{orderId}/confirm", ConfirmOrderAsync)
            .WithName("ConfirmOrder");
    }

    /// <summary>
    /// Read value and ETag together, mutate, write conditionally, and on a lost race read
    /// again before retrying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things about this loop are Dapr-specific and easy to get wrong.
    /// </para>
    /// <para>
    /// <b>The conflict is a <c>bool</c>, not an exception.</b> <c>TrySaveStateAsync</c> returns
    /// <c>false</c> when the store's current ETag no longer matches the one presented. On the
    /// raw HTTP API the same conflict is a 500-class body with <c>ERR_STATE_SAVE</c> and
    /// "possible etag mismatch" in the text, which is considerably less pleasant to branch on.
    /// The non-<c>Try</c> <c>SaveStateAsync</c> takes no ETag at all, so optimistic concurrency
    /// is opt-in by method choice.
    /// </para>
    /// <para>
    /// <b>The re-read is the whole point.</b> Retrying the save with the ETag from the previous
    /// attempt cannot ever succeed: that ETag is exactly the one the store has already moved
    /// past. The loop re-reads at the top of every attempt, so each attempt carries a fresh
    /// ETag. Hoisting the read out of the loop turns this into an infinite retry.
    /// </para>
    /// <para>
    /// <b>Consistency is a separate axis from concurrency.</b> Dapr assumes eventually
    /// consistent stores by default, so under <c>ConsistencyMode.Eventual</c> the ETag read
    /// here may already be behind the authoritative copy and a "successful" conditional write
    /// guarantees less than it looks like it does. <c>Strong</c> on the read is what makes the
    /// comparison mean what the code says it means.
    /// </para>
    /// <para>
    /// Both halves of the cycle carry <see cref="OrderPartition"/>, and a read and a conditional
    /// write of the same key that disagree about the partition is the one way to make this loop
    /// spin forever against Cosmos DB while passing every test on Redis.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ConfirmOrderAsync(
        string customerId,
        string orderId,
        DaprClient dapr,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dapr);

        var partition = OrderPartition.For(customerId);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (order, etag) = await dapr.GetStateAndETagAsync<Order>(
                StateStore.Name,
                orderId,
                ConsistencyMode.Strong,
                partition,
                cancellationToken);

            // A missing key comes back as a default value plus some ETag rather than throwing,
            // so the null check has to come before anything reads the order. The customer check
            // beside it is what Redis will not do for you: on Cosmos DB an order belonging to
            // someone else is simply in another partition and never arrives.
            if (order is null || !string.Equals(order.CustomerId, customerId, StringComparison.Ordinal))
            {
                return Results.NotFound();
            }

            if (order.Status is OrderStatus.Confirmed)
            {
                // Already done. Re-confirming is a no-op, which is what makes this endpoint
                // safe to retry from the outside as well as the inside.
                return Results.Ok(order);
            }

            var confirmed = order with
            {
                Status = OrderStatus.Confirmed,
                ConfirmedAt = DateTimeOffset.UtcNow,
            };

            var saved = await dapr.TrySaveStateAsync(
                StateStore.Name,
                orderId,
                confirmed,
                etag,
                new StateOptions
                {
                    // With an ETag attached the store already behaves first-write-wins; saying
                    // so explicitly keeps the intent readable next to the ETag itself.
                    Concurrency = ConcurrencyMode.FirstWrite,
                    Consistency = ConsistencyMode.Strong,
                },
                partition,
                cancellationToken);

            if (saved)
            {
                logger.LogInformation("Confirmed order {OrderId} on attempt {Attempt}.", orderId, attempt);

                return Results.Ok(confirmed);
            }

            logger.LogInformation(
                "Lost the ETag race on order {OrderId}, attempt {Attempt} of {MaxAttempts}.",
                orderId, attempt, MaxAttempts);

            // A little backoff. A tight loop under contention just restates the race at speed.
            await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
        }

        logger.LogWarning("Gave up confirming order {OrderId} after {MaxAttempts} attempts.", orderId, MaxAttempts);

        return Results.Conflict(new ConfirmConflictResponse(orderId, MaxAttempts));
    }
}

/// <summary>Body returned when the confirm lost the race too many times in a row.</summary>
/// <param name="OrderId">The order that could not be confirmed.</param>
/// <param name="Attempts">How many read-modify-write cycles were tried.</param>
public sealed record ConfirmConflictResponse(string OrderId, int Attempts);

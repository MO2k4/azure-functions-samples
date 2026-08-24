namespace OrderApi.Orders;

/// <summary>
/// Component <c>metadata.name</c> of the state store. Identical in
/// <c>components/local/statestore.redis.yaml</c> and <c>components/azure/statestore.cosmos.yaml</c>,
/// which is the point: the code names a component, never a host or a connection string.
/// </summary>
public static class StateStore
{
    /// <summary>The one string the application knows about its own database.</summary>
    public const string Name = "orderstore";
}

/// <summary>
/// The Cosmos DB partition every state call on an order has to name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> For non-actor state the Cosmos DB component uses the item's own
/// physical state key as the partition key value, and that key is <c>orders-api||{OrderId}</c>:
/// the app ID prefix is added by the sidecar, so application code cannot reproduce the default
/// without hardcoding its own app ID. An explicit <c>partitionKey</c> in the metadata bag can
/// therefore never equal the default. It always relocates the document.
/// </para>
/// <para>
/// <b>Why every slice uses it.</b> Cancelling an order writes the order and its audit record in
/// one <c>ExecuteStateTransactionAsync</c>, and Cosmos DB will not commit a transaction whose
/// items span partitions, so that call has no choice but to pin both keys to one value. Once one
/// write pins the order key, every other read and write of it has to pin the same value or Cosmos
/// DB looks in the partition the state key implies and finds nothing. Half-applying the override
/// is worse than not applying it: the create, get and confirm paths would keep using the order
/// key's own partition while the cancel path moved the document into the customer's, leaving two
/// documents with the same order ID and a stale read on the way out.
/// </para>
/// <para>
/// <b>Why the customer ID.</b> It is the value that puts an order and its audit record together,
/// it is stable for the life of the order, and it is the field a real Cosmos DB container would
/// be partitioned on anyway. It is also the reason the routes carry <c>{customerId}</c>: a read
/// has to name the partition before it can read the document that holds it, so the partition
/// value has to arrive from the caller rather than from the record.
/// </para>
/// <para>
/// <b>Redis ignores all of this.</b> One keyspace, no partitions, the metadata entry is dropped.
/// That is what makes the mistake quiet: the version that half-applies the override runs
/// perfectly on a laptop for as long as you care to test it.
/// </para>
/// </remarks>
public static class OrderPartition
{
    /// <summary>The metadata key the Cosmos DB state store reads the partition value from.</summary>
    private const string MetadataKey = "partitionKey";

    /// <summary>Builds the metadata bag that every state call on an order key carries.</summary>
    /// <param name="customerId">The customer the order belongs to, and the partition value.</param>
    public static IReadOnlyDictionary<string, string> For(string customerId) =>
        new Dictionary<string, string> { [MetadataKey] = customerId };
}

/// <summary>A single line on an order.</summary>
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>
/// The order as it is stored in the Dapr state store. Dapr writes it under the physical key
/// <c>orders-api||{OrderId}</c>: the sidecar, not the SDK, adds that prefix. On Cosmos DB the
/// document lives in the customer's partition rather than that key's own, because every slice
/// passes <see cref="OrderPartition"/>.
/// </summary>
public sealed record Order(
    string OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ConfirmedAt);

/// <summary>Lifecycle of an order in this sample.</summary>
public enum OrderStatus
{
    /// <summary>Stock was reserved and the order was written to the state store.</summary>
    Placed,

    /// <summary>The ETag-guarded confirm succeeded.</summary>
    Confirmed,

    /// <summary>The order and its audit record were written in one state transaction.</summary>
    Cancelled,
}

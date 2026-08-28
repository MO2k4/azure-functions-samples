namespace DaprAspireDemo.OrderService.Orders;

/// <summary>
/// Component <c>metadata.name</c> of the state store. It is the string
/// <c>builder.AddDaprStateStore("statestore")</c> in the AppHost produces, and the only thing this
/// application knows about its own database.
/// </summary>
public static class StateStore
{
    /// <summary>The one string the application knows about its own database.</summary>
    public const string Name = "statestore";
}

/// <summary>A single line on an order.</summary>
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>
/// The order as it is stored in the Dapr state store. Dapr writes it under the physical key
/// <c>order-service||{OrderId}</c>: the sidecar, not the SDK, adds that app ID prefix.
/// </summary>
public sealed record Order(
    string OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset PlacedAt);

/// <summary>Lifecycle of an order in this sample.</summary>
public enum OrderStatus
{
    /// <summary>Stock was available and the order was written to the state store.</summary>
    Placed,

    /// <summary>The ETag-guarded confirm succeeded.</summary>
    Confirmed,
}

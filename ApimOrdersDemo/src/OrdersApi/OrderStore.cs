using System.Collections.Concurrent;

namespace OrdersApi;

// In-memory and per instance: orders vanish when Flex Consumption scales to zero; swap for Table Storage or Cosmos DB when the data matters.
public sealed class OrderStore(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Order> _orders = new(StringComparer.OrdinalIgnoreCase);

    public Order Create(CreateOrderRequest request)
    {
        var order = new Order(
            OrderId: $"ORD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CustomerId: request.CustomerId,
            Lines: request.Lines,
            Total: request.Lines.Sum(line => line.Quantity * line.UnitPrice),
            Status: OrderStatus.Pending,
            CreatedAt: clock.GetUtcNow());

        _orders[order.OrderId] = order;
        return order;
    }

    public Order? Find(string orderId) => _orders.GetValueOrDefault(orderId);

    public IReadOnlyList<Order> List(OrderStatus? status) =>
        [.. _orders.Values
            .Where(order => status is null || order.Status == status)
            .OrderByDescending(order => order.CreatedAt)];
}

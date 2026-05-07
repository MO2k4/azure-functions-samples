using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;

namespace OrderProcessor.Core.Stores;

public sealed class SqlOrderStore(ILogger<SqlOrderStore> logger) : IOrderStore
{
    private readonly ConcurrentDictionary<string, Order> _store = new();

    public Task<Order?> GetAsync(string orderId, CancellationToken cancellationToken)
    {
        logger.LogInformation("SqlOrderStore: Get {OrderId}", orderId);
        _store.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task SaveAsync(Order order, CancellationToken cancellationToken)
    {
        logger.LogInformation("SqlOrderStore: Save {OrderId}", order.OrderId);
        _store[order.OrderId] = order;
        return Task.CompletedTask;
    }
}

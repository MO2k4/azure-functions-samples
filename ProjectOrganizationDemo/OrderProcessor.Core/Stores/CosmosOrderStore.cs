using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;

namespace OrderProcessor.Core.Stores;

public sealed class CosmosOrderStore(ILogger<CosmosOrderStore> logger) : IOrderStore
{
    private readonly ConcurrentDictionary<string, Order> _store = new();

    public Task<Order?> GetAsync(string orderId, CancellationToken cancellationToken)
    {
        logger.LogInformation("CosmosOrderStore: Get {OrderId}", orderId);
        _store.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task SaveAsync(Order order, CancellationToken cancellationToken)
    {
        logger.LogInformation("CosmosOrderStore: Save {OrderId}", order.OrderId);
        _store[order.OrderId] = order;
        return Task.CompletedTask;
    }
}

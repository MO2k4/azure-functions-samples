using OrderProcessor.Core.Models;

namespace OrderProcessor.Core.Stores;

public interface IOrderStore
{
    Task<Order?> GetAsync(string orderId, CancellationToken cancellationToken);
    Task SaveAsync(Order order, CancellationToken cancellationToken);
}

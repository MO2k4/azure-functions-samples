using HttpTriggerDemo.Logging;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo;

public record Order(string OrderId, string ProductId, int Quantity);

public record OrderResult(bool IsSuccess, Order? Order, string? Error)
{
    public static OrderResult Success(Order order) => new(true, order, null);
    public static OrderResult Failure(string error) => new(false, null, error);
}

public interface IOrderRepository
{
    Task SaveAsync(Order order);
}

public interface IOrderService
{
    Task<OrderResult> CreateOrderAsync(CreateOrderRequest request);
}

public class OrderService(ILogger<OrderService> logger, IOrderRepository repository) : IOrderService
{
    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
        {
            OrderLogs.OrderValidationFailed(logger, "Quantity must be greater than zero");
            return OrderResult.Failure("Quantity must be greater than zero");
        }

        var order = new Order(
            OrderId: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            ProductId: request.ProductId,
            Quantity: request.Quantity);

        await repository.SaveAsync(order);
        OrderLogs.OrderSaved(logger);

        OrderLogs.OrderCreated(logger, order.OrderId, order.ProductId);

        return OrderResult.Success(order);
    }
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    public Task SaveAsync(Order order) => Task.CompletedTask;
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;
using OrderProcessor.Core.Stores;
using OrderProcessor.Core.Validators;

namespace OrderProcessor.Queue.Functions;

// The Queue app picks the Sql store via [FromKeyedServices].
// Same Core library, same OrderValidator, different scaling unit and deploy cadence
// from the Http app — that's the whole point of splitting.
public sealed class ProcessOrderFunction(
    ILogger<ProcessOrderFunction> logger,
    OrderValidator validator,
    [FromKeyedServices(OrderStoreKeys.Sql)] IOrderStore store)
{
    [Function(nameof(ProcessOrder))]
    public async Task ProcessOrder(
        [QueueTrigger("orders")] OrderMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Process order {OrderId}", message.OrderId);

        var order = new Order(message.OrderId, message.CustomerId, message.Amount, OrderStatus.Confirmed);

        var validation = validator.Validate(order);
        if (!validation.IsValid)
        {
            logger.LogWarning("Invalid order {OrderId}: {Error}", order.OrderId, validation.Error);
            return;
        }

        await store.SaveAsync(order, cancellationToken);
    }
}

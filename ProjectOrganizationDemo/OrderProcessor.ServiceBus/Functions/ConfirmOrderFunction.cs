using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;
using OrderProcessor.Core.Validators;
using StackExchange.Redis;

// StackExchange.Redis also defines an `Order` type; alias the domain model we mean.
using Order = OrderProcessor.Core.Models.Order;

namespace OrderProcessor.ServiceBus.Functions;

// Consumes the Service Bus "orders" queue and writes a receipt blob to app-storage.
// Three Aspire resources feed this one function:
//   - messaging (Service Bus): the [ServiceBusTrigger] Connection, auto-wired by WithReference
//   - cache (Redis): IConnectionMultiplexer via AddRedisClient, used here for idempotency
//   - receipts (Blob, app-storage): the [BlobOutput] Connection, auto-wired by WithReference
public sealed class ConfirmOrderFunction(
    ILogger<ConfirmOrderFunction> logger,
    OrderValidator validator,
    IConnectionMultiplexer redis)
{
    [Function(nameof(ConfirmOrder))]
    [BlobOutput("receipts/{OrderId}.json", Connection = "receipts")]
    public async Task<Order?> ConfirmOrder(
        [ServiceBusTrigger("orders", Connection = "messaging")] OrderMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Confirm order {OrderId}", message.OrderId);

        // Service Bus delivers at-least-once, so a retry or duplicate send can replay
        // the same order. SET NX on Redis gives us a cheap idempotency gate: the first
        // delivery writes the key, later ones see it already exists and skip the receipt.
        var db = redis.GetDatabase();
        var firstDelivery = await db.StringSetAsync(
            $"orders:seen:{message.OrderId}", "1", when: When.NotExists);
        if (!firstDelivery)
        {
            logger.LogWarning("Duplicate order {OrderId}, skipping receipt", message.OrderId);
            return null;
        }

        var order = new Order(message.OrderId, message.CustomerId, message.Amount, OrderStatus.Confirmed);

        var validation = validator.Validate(order);
        if (!validation.IsValid)
        {
            logger.LogWarning("Invalid order {OrderId}: {Error}", order.OrderId, validation.Error);
            return null;
        }

        // Returning the order serializes it to receipts/{OrderId}.json in app-storage.
        return order;
    }
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TriggerDemo;

public record OrderMessage(string OrderId, string CustomerId, decimal Amount);

public class OrderProcessor(ILogger<OrderProcessor> logger)
{
    // Reads from the "orders" queue and writes to the "notifications" queue.
    // The return value becomes the output message via [QueueOutput].
    // Change the return type to Task<string> and await inside if you need async work.
    [Function(nameof(OrderProcessor))]
    [QueueOutput("notifications", Connection = "AzureWebJobsStorage")]
    public string Run(
        [QueueTrigger("orders", Connection = "AzureWebJobsStorage")]
        OrderMessage order)
    {
        logger.LogInformation(
            "Processing order {OrderId} for {CustomerId}: {Amount:C}",
            order.OrderId, order.CustomerId, order.Amount);

        // Process order...

        return $"Order {order.OrderId} processed for {order.CustomerId}";
    }
}

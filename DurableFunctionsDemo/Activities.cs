using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableFunctionsDemo;

// Activities are the only place I/O and side effects are allowed. They bind
// directly to their input type. Guarantee is at-least-once, so keep them idempotent.

public static class ValidateOrderActivity
{
    [Function(nameof(ValidateOrderActivity))]
    public static bool Run([ActivityTrigger] OrderRequest order)
    {
        // Real work and I/O belong here, never in the orchestrator:
        // check inventory, validate the customer, hit the database.
        return order.Quantity > 0 && !string.IsNullOrWhiteSpace(order.Sku);
    }
}

public static class CreateOrderActivity
{
    [Function(nameof(CreateOrderActivity))]
    public static string Run([ActivityTrigger] OrderRequest order)
    {
        // Persist the order; return the new order ID. A real implementation would
        // write to a database. The ID must come from the activity (a side effect),
        // not be generated in the orchestrator.
        return $"ORD-{order.CustomerId}-{order.Sku}";
    }
}

public static class SendConfirmationActivity
{
    [Function(nameof(SendConfirmationActivity))]
    public static void Run([ActivityTrigger] string orderId, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(SendConfirmationActivity));
        logger.LogInformation("Confirmation sent for order {OrderId}", orderId);
    }
}

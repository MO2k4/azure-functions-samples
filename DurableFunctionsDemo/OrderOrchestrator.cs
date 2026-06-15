using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace DurableFunctionsDemo;

public static class OrderOrchestrator
{
    [Function(nameof(OrderOrchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var order = context.GetInput<OrderRequest>()!;

        // Monday-tip API shape: deterministic, replay-safe substitutes for
        // DateTime.UtcNow and Guid.NewGuid() inside an orchestrator. Both return
        // the same value on every replay.
        DateTime startedUtc = context.CurrentUtcDateTime;
        Guid traceId = context.NewGuid();

        // Wednesday-tip API shape: retry policy on a flaky activity.
        // Attempts FIRST, then interval (isolated-worker RetryPolicy ctor order).
        var validateOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));

        var validated = await context.CallActivityAsync<bool>(
            nameof(ValidateOrderActivity), order, options: validateOptions);
        if (!validated)
            return "Order validation failed";

        var orderId = await context.CallActivityAsync<string>(
            nameof(CreateOrderActivity), order);

        await context.CallActivityAsync(
            nameof(SendConfirmationActivity), orderId);

        // Fold the replay-safe values into the result so they are observably used.
        return $"{orderId} (trace {traceId:N}, started {startedUtc:O})";
    }
}

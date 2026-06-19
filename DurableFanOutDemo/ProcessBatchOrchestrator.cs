using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace DurableFanOutDemo;

public static class ProcessBatchOrchestrator
{
    [Function(nameof(ProcessBatchOrchestrator))]
    public static async Task<BatchSummary> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var items = context.GetInput<OrderItem[]>()!;

        // Fan-out: schedule every item at once, collect the tasks unawaited.
        Task<OrderResult>[] tasks =
            [.. items.Select(item => context.CallActivityAsync<OrderResult>(
                nameof(ProcessItemActivity), item))];

        // Fan-in: one await blocks until all of them complete.
        OrderResult[] results = await Task.WhenAll(tasks);

        // Optional: hand the collected results to a final aggregation activity.
        return await context.CallActivityAsync<BatchSummary>(
            nameof(SummarizeBatchActivity), results);
    }
}

using Microsoft.Azure.Functions.Worker;

namespace DurableFanOutDemo;

// Activities are the only place I/O and side effects are allowed. The fan-out
// schedules many of these at once; the fan-in collects their results.
public static class ProcessItemActivity
{
    [Function(nameof(ProcessItemActivity))]
    public static OrderResult Run([ActivityTrigger] OrderItem item)
    {
        // Real work and I/O belong here: check stock, price, reserve inventory.
        bool reserved = item.Quantity > 0;
        decimal lineTotal = item.Quantity * 9.99m;
        return new OrderResult(item.Sku, reserved, lineTotal);
    }
}

// The final aggregation step the orchestrator fans in to. It folds the per-item
// results into a single small summary so the orchestration output stays compact.
public static class SummarizeBatchActivity
{
    [Function(nameof(SummarizeBatchActivity))]
    public static BatchSummary Run([ActivityTrigger] OrderResult[] results) =>
        new(
            Processed: results.Length,
            Reserved: results.Count(r => r.Reserved),
            Total: results.Sum(r => r.LineTotal));
}

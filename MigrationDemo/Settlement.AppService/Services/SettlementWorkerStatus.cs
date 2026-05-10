using System.Collections.Concurrent;
using Settlement.Core.Models;

namespace Settlement.AppService.Services;

// Backs the /status endpoint so an operator can check what the worker is
// currently chewing on without tailing logs. This is one capability that does
// not exist for the Function App variant — the trigger has no shared state.
public sealed class SettlementWorkerStatus
{
    private readonly ConcurrentDictionary<string, BatchStatus> _batches = new();

    public IReadOnlyCollection<BatchStatus> Snapshot() => [.. _batches.Values];

    public void Update(string batchId, SettlementProgress progress) =>
        _batches.AddOrUpdate(
            batchId,
            _ => new BatchStatus(batchId, progress, IsComplete: false, UpdatedUtc: DateTimeOffset.UtcNow),
            (_, existing) => existing with { Progress = progress, UpdatedUtc = DateTimeOffset.UtcNow });

    public void Complete(string batchId, SettlementProgress final) =>
        _batches[batchId] = new BatchStatus(batchId, final, IsComplete: true, UpdatedUtc: DateTimeOffset.UtcNow);
}

public sealed record BatchStatus(
    string BatchId,
    SettlementProgress Progress,
    bool IsComplete,
    DateTimeOffset UpdatedUtc);

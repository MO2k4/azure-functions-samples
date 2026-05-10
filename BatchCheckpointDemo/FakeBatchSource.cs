using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

namespace BatchCheckpointDemo;

public sealed class FakeBatchSource(IConfiguration configuration) : IBatchSource
{
    private readonly int _totalItems = configuration.GetValue("TotalItems", 10_000);
    private readonly int _perChunkDelayMs = configuration.GetValue("PerChunkDelayMs", 200);

    public async IAsyncEnumerable<BatchChunk> ChunksAfterAsync(
        string batchId,
        int lastCommittedItemId,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var next = lastCommittedItemId + 1;

        while (next <= _totalItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var last = Math.Min(next + chunkSize - 1, _totalItems);
            var items = Enumerable.Range(next, last - next + 1).ToArray();

            await Task.Delay(_perChunkDelayMs, cancellationToken);

            yield return new BatchChunk(next, last, items);
            next = last + 1;
        }
    }
}

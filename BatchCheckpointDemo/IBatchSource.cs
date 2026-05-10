namespace BatchCheckpointDemo;

public interface IBatchSource
{
    IAsyncEnumerable<BatchChunk> ChunksAfterAsync(
        string batchId,
        int lastCommittedItemId,
        int chunkSize,
        CancellationToken cancellationToken);
}

public sealed record BatchChunk(int FirstItemId, int LastItemId, IReadOnlyList<int> Items);

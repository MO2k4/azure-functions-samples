using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BatchCheckpointDemo;

// Long-running queue handler that survives a worker restart by checkpointing per chunk.
// On retry the function re-reads the cursor blob and resumes after the last committed chunk
// instead of restarting from item 1.
public sealed class DrainBatchFunction(
    BlobContainerClient cursors,
    IBatchSource source,
    IConfiguration configuration,
    ILogger<DrainBatchFunction> logger)
{
    private readonly int _chunkSize = configuration.GetValue("BatchSize", 500);

    [Function(nameof(DrainBatchFunction))]
    public async Task Run(
        [QueueTrigger("batches", Connection = "AzureWebJobsStorage")] BatchCommand command,
        CancellationToken cancellationToken)
    {
        await cursors.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var checkpoint = cursors.GetBlobClient($"batch-{command.BatchId}.cursor");
        var lastCommitted = await ReadCursorAsync(checkpoint, cancellationToken);

        logger.LogInformation(
            "Draining batch {BatchId}: total={Total}, resumeAfter={Cursor}",
            command.BatchId, command.TotalItems, lastCommitted);

        await foreach (var chunk in source.ChunksAfterAsync(
                           command.BatchId, lastCommitted, _chunkSize, cancellationToken))
        {
            await ProcessAsync(chunk, cancellationToken);

            // Commit the cursor *before* the next chunk; if the worker is killed
            // immediately after this upload, the next attempt resumes here.
            await checkpoint.UploadAsync(
                BinaryData.FromString(chunk.LastItemId.ToString(CultureInfo.InvariantCulture)),
                overwrite: true,
                cancellationToken);

            logger.LogInformation(
                "Committed batch {BatchId} up to item {LastId}",
                command.BatchId, chunk.LastItemId);
        }

        logger.LogInformation("Batch {BatchId} drained", command.BatchId);
    }

    private static async Task<int> ReadCursorAsync(BlobClient checkpoint, CancellationToken ct)
    {
        try
        {
            var response = await checkpoint.DownloadContentAsync(ct);
            return int.Parse(response.Value.Content.ToString(), CultureInfo.InvariantCulture);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    private async Task ProcessAsync(BatchChunk chunk, CancellationToken ct)
    {
        // Stand-in for real work (DB write, downstream API call, file emit).
        // Stays I/O-shaped so the cancellation token is honoured.
        await Task.Delay(50, ct);
        logger.LogDebug("Processed items {First}..{Last} ({Count})",
            chunk.FirstItemId, chunk.LastItemId, chunk.Items.Count);
    }
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Settlement.Core.Models;
using Settlement.Core.Services;

namespace Settlement.FunctionApp;

// Function-app entrypoint. Receives the SettlementBatch from the queue and
// hands it to the shared IPaymentSettler. The host's job ends at the call site.
//
// Why this is the variant most likely to outgrow the platform:
//   - Consumption plan caps execution at 10 minutes; large batches (e.g. 50k
//     payments at 50ms each) exceed that and the host kills the worker.
//     The trigger sees the abort as a failure, increments the dequeue count,
//     and ships the same message back to be processed from scratch.
//   - The fix inside Functions is checkpointing (see BatchCheckpointDemo).
//     The fix outside Functions is one of the other hosts in this folder.
public sealed class SettlementFunction(
    IPaymentSettler settler,
    ILogger<SettlementFunction> logger)
{
    [Function(nameof(SettlementFunction))]
    public async Task Run(
        [QueueTrigger("settlement-batches", Connection = "AzureWebJobsStorage")] SettlementBatch batch,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Function host received batch {BatchId} ({Count} payments)",
            batch.BatchId, batch.Payments.Count);

        var result = await settler.SettleAsync(batch, progress: null, cancellationToken);

        logger.LogInformation(
            "Function host completed batch {BatchId}: settled={Settled}, failed={Failed}",
            batch.BatchId, result.Settled, result.Failed);
    }
}

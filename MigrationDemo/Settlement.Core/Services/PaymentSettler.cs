using Microsoft.Extensions.Logging;
using Settlement.Core.Models;

namespace Settlement.Core.Services;

public sealed class PaymentSettler(
    ISettlementGateway gateway,
    ILogger<PaymentSettler> logger) : IPaymentSettler
{
    public async Task<SettlementProgress> SettleAsync(
        SettlementBatch batch,
        IProgress<SettlementProgress>? progress,
        CancellationToken cancellationToken)
    {
        var settled = 0;
        var failed = 0;
        var total = batch.Payments.Count;

        logger.LogInformation(
            "Settling batch {BatchId}: total={Total}, cutoff={CutoffUtc:O}",
            batch.BatchId, total, batch.CutoffUtc);

        foreach (var payment in batch.Payments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await gateway.SubmitAsync(payment, cancellationToken);

            if (response.Accepted)
            {
                settled++;
            }
            else
            {
                failed++;
                logger.LogWarning(
                    "Settlement rejected for {PaymentId}: {ReasonCode}",
                    payment.PaymentId, response.ReasonCode);
            }

            progress?.Report(new SettlementProgress(settled, failed, total));
        }

        var final = new SettlementProgress(settled, failed, total);
        logger.LogInformation(
            "Batch {BatchId} settled: settled={Settled}, failed={Failed}",
            batch.BatchId, final.Settled, final.Failed);
        return final;
    }
}

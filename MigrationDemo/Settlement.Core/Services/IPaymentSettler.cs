using Settlement.Core.Models;

namespace Settlement.Core.Services;

// The shared workload. All three hosts (Function App, App Service, Container App)
// invoke this same call. The host's only job is to receive a SettlementBatch
// from somewhere (queue trigger, BackgroundService, job entrypoint) and pass it
// in. Hosting concerns end at this boundary.
public interface IPaymentSettler
{
    Task<SettlementProgress> SettleAsync(
        SettlementBatch batch,
        IProgress<SettlementProgress>? progress,
        CancellationToken cancellationToken);
}

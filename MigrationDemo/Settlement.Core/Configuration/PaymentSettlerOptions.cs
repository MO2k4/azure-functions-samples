using System.ComponentModel.DataAnnotations;

namespace Settlement.Core.Configuration;

// Workload-shape options for the PaymentSettler itself. Distinct from
// SettlementOptions, which is gateway-shape (per-payment latency, fake
// failure rate). Keeping the two split lets a future real gateway add its
// own knobs without bleeding into the settler's contract.
public sealed class PaymentSettlerOptions
{
    public const string SectionName = "PaymentSettler";

    // Throttles IProgress<SettlementProgress> notifications when batches
    // are large. Default 1 keeps the existing per-payment behaviour; set
    // to N to report after every N-th payment (and once at the end).
    [Range(1, 100_000)]
    public int ProgressReportInterval { get; init; } = 1;
}

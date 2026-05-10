using System.ComponentModel.DataAnnotations;

namespace Settlement.Core.Configuration;

public sealed class SettlementOptions
{
    public const string SectionName = "Settlement";

    [Range(1, 10_000)]
    public int PerPaymentDelayMs { get; init; } = 50;

    [Range(0.0, 1.0)]
    public double FailureRate { get; init; } = 0.02;
}

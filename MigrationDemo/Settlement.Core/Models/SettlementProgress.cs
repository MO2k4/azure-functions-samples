namespace Settlement.Core.Models;

public sealed record SettlementProgress(
    int Settled,
    int Failed,
    int Total)
{
    public int Processed => Settled + Failed;

    public bool IsComplete => Processed >= Total;
}

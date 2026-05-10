namespace Settlement.Core.Models;

public sealed record SettlementBatch(
    string BatchId,
    DateTimeOffset CutoffUtc,
    IReadOnlyList<Payment> Payments);

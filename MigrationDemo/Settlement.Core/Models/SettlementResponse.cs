namespace Settlement.Core.Models;

public sealed record SettlementResponse(
    string PaymentId,
    bool Accepted,
    string? ReasonCode);

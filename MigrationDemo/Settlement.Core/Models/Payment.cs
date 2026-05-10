namespace Settlement.Core.Models;

public sealed record Payment(
    string PaymentId,
    decimal Amount,
    string Currency);

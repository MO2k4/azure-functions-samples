namespace DurableFunctionsDemo;

// A dependency an activity reaches out to (a real one would call a payment provider
// over HTTP). Injected into PaymentActivities so a test can substitute a fake and
// assert on the interaction instead of charging a real card.
public interface IPaymentGateway
{
    Task<bool> ChargeAsync(string customerId, int quantity);
}

// Default implementation registered in Program.cs. Stands in for a payment provider
// SDK; keeps the sample runnable without external credentials.
public sealed class PaymentGateway : IPaymentGateway
{
    public Task<bool> ChargeAsync(string customerId, int quantity)
        => Task.FromResult(!string.IsNullOrWhiteSpace(customerId) && quantity > 0);
}

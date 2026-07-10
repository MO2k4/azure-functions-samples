using Microsoft.Azure.Functions.Worker;

namespace DurableFunctionsDemo;

// An activity with a dependency. The isolated worker resolves the constructor
// from the DI container (IPaymentGateway is registered in Program.cs), so the
// class is instance-based rather than static. That constructor is the seam a
// unit test uses: hand it a mocked gateway and assert on what ChargeCard does.
public class PaymentActivities(IPaymentGateway gateway)
{
    [Function(nameof(ChargeCard))]
    public Task<bool> ChargeCard([ActivityTrigger] OrderRequest order)
        => gateway.ChargeAsync(order.CustomerId, order.Quantity);
}

using Microsoft.Extensions.Options;
using Settlement.Core.Configuration;
using Settlement.Core.Models;

namespace Settlement.Core.Services;

// Simulates a downstream payments API: per-payment latency plus a configurable
// failure rate. Lets all three host variants run end-to-end without any external
// dependency. Swap with a typed HttpClient in production.
public sealed class FakeSettlementGateway(IOptions<SettlementOptions> options) : ISettlementGateway
{
    private readonly SettlementOptions _options = options.Value;

    public async Task<SettlementResponse> SubmitAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        await Task.Delay(_options.PerPaymentDelayMs, cancellationToken);

        // Deterministic pseudo-random based on the payment id so the same demo
        // batch produces the same outcome across hosts.
        var hash = (uint)payment.PaymentId.GetHashCode(StringComparison.Ordinal);
        var roll = (hash & 0xFFFF) / 65536.0;
        var accepted = roll >= _options.FailureRate;

        return new SettlementResponse(
            payment.PaymentId,
            accepted,
            accepted ? null : "GATEWAY_DECLINED");
    }
}

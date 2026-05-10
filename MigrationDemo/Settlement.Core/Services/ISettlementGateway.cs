using Settlement.Core.Models;

namespace Settlement.Core.Services;

// Stand-in for the downstream payments network the settler talks to.
// Each host swaps in a real implementation (typed HttpClient via IHttpClientFactory)
// at the composition root.
public interface ISettlementGateway
{
    Task<SettlementResponse> SubmitAsync(
        Payment payment,
        CancellationToken cancellationToken);
}

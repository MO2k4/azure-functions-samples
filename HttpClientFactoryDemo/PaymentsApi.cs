using System.Net.Http.Json;

namespace HttpClientFactoryDemo;

public interface IPaymentsApi
{
    Task<AuthorizeResult> AuthorizeAsync(AuthorizeCommand command, CancellationToken cancellationToken);
}

public sealed record AuthorizeCommand(string OrderId, string CustomerId, decimal Amount, string Currency);

public sealed record AuthorizeResult(string Reference, string Status);

public sealed class PaymentsApi(HttpClient http) : IPaymentsApi
{
    public async Task<AuthorizeResult> AuthorizeAsync(AuthorizeCommand command, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("authorizations", command, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthorizeResult>(cancellationToken)
            ?? throw new InvalidOperationException("Empty payments response");
    }
}

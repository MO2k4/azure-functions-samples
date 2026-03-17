using System.Net.Http;

namespace EventHubDemo.Tests;

// The vnext-preview Cosmos emulator serves plain HTTP on port 8081.
// Testcontainers' built-in UriRewriter keeps the HTTPS scheme when rewriting to
// the mapped port. This handler rewrites every outgoing request to
// http://localhost:{mappedPort} so the Cosmos SDK (which discovers self-referential
// endpoints at localhost:8081 from the account response) reaches the container.
internal sealed class CosmosEmulatorHandler(int mappedPort) : DelegatingHandler(new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
})
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
            request.RequestUri = new UriBuilder(request.RequestUri) { Scheme = "http", Port = mappedPort }.Uri;
        return base.SendAsync(request, cancellationToken);
    }
}

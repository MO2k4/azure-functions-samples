using OrderPipelineDemo.Tests;
using Testcontainers.Azurite;

[assembly: AssemblyFixture(typeof(AzuriteFixture))]

namespace OrderPipelineDemo.Tests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
            .WithInMemoryPersistence()
            // Azure.Storage.Blobs 12.30 and Queues 12.28 send API version 2026-10-06; Azurite 3.37.0 stops at 2026-06-06.
            .WithCommand("--skipApiVersionCheck")
            .Build();

    public string GetConnectionString() => _azurite.GetConnectionString();

    // Lowercase hex, so it's valid for queue and container names.
    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync() => await _azurite.StartAsync();

    public async ValueTask DisposeAsync() => await _azurite.DisposeAsync();
}

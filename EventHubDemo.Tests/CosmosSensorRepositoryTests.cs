using DotNet.Testcontainers.Builders;
using Microsoft.Azure.Cosmos;
using System.Net.Http;
using Testcontainers.CosmosDb;

namespace EventHubDemo.Tests;

public class CosmosSensorRepositoryTests : IAsyncLifetime
{
    private readonly CosmosDbContainer _cosmos = new CosmosDbBuilder()
        .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilMessageIsLogged("Gateway=OK, Explorer=OK"))
        .Build();

    private CosmosClient _client = null!;
    private Container _container = null!;

    public async Task InitializeAsync()
    {
        await _cosmos.StartAsync();

        var mappedPort = _cosmos.GetMappedPublicPort(8081);

        _client = new CosmosClient(
            _cosmos.GetConnectionString(),
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => new HttpClient(new CosmosEmulatorHandler(mappedPort)),
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

        var database = await _client.CreateDatabaseIfNotExistsAsync("SensorData");
        _container = (await database.Database.CreateContainerIfNotExistsAsync(
            "readings", "/deviceId")).Container;
    }

    [Fact]
    public async Task SaveAsync_WithValidReading_PersistsDocument()
    {
        var repository = new CosmosSensorRepository(_client);
        var reading = new SensorReading("device-01", 22.5, 60.0, DateTimeOffset.UtcNow);

        await repository.SaveAsync(reading);

        var query = _container.GetItemQueryIterator<dynamic>(
            "SELECT * FROM c WHERE c.deviceId = 'device-01'");
        var results = new List<dynamic>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync();
            results.AddRange(page);
        }

        Assert.Single(results);
        Assert.Equal(22.5, (double)results[0].temperature);
        Assert.Equal(60.0, (double)results[0].humidity);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _cosmos.DisposeAsync();
    }
}

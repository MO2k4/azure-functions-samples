using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Reflection;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.EventHubs;

namespace EventHubDemo.Tests;

public sealed class SensorPipelineFixture : IAsyncLifetime
{
    public const string Name = "SensorPipeline";

    // AzureWebJobsStorage for the Functions host (separate from the EventHubs emulator's internal Azurite)
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();

    private readonly CosmosDbContainer _cosmos = new CosmosDbBuilder()
        .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
        .Build();

    private readonly EventHubsContainer _eventHubs;

    private Process? _funcProcess;

    public CosmosClient CosmosClient { get; private set; } = null!;
    public EventHubProducerClient ProducerClient { get; private set; } = null!;

    public SensorPipelineFixture()
    {
        var configPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "eventhubs-config.json");

        var configBytes = File.ReadAllBytes(configPath);

        _eventHubs = new EventHubsBuilder()
            .WithAcceptLicenseAgreement(true)
            .WithResourceMapping(configBytes, "/Eventhubs_Emulator/ConfigFiles/Config.json")
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        await Task.WhenAll(
            _azurite.StartAsync(),
            _cosmos.StartAsync(),
            _eventHubs.StartAsync());

        CosmosClient = new CosmosClient(
            _cosmos.GetConnectionString(),
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => _cosmos.HttpClient,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

        // Create database and container before the function starts writing
        var db = await CosmosClient.CreateDatabaseIfNotExistsAsync("SensorData");
        await db.Database.CreateContainerIfNotExistsAsync("readings", "/deviceId");

        ProducerClient = new EventHubProducerClient(
            _eventHubs.GetConnectionString(),
            "sensor-readings");

        // Locate the EventHubDemo project directory
        var gitRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
        var functionAppDir = Path.Combine(gitRoot, "EventHubDemo");

        var env = new Dictionary<string, string?>
        {
            ["AzureWebJobsStorage"] = _azurite.GetConnectionString(),
            ["EventHubConnection"] = _eventHubs.GetConnectionString(),
            ["CosmosDbConnection"] = _cosmos.GetConnectionString(),
            ["CosmosDatabase"] = "SensorData",
            ["CosmosContainer"] = "readings",
            ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
        };

        var startInfo = new ProcessStartInfo("func", "start")
        {
            WorkingDirectory = functionAppDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var (key, value) in env)
            startInfo.Environment[key] = value;

        _funcProcess = Process.Start(startInfo)!;

        await WaitForFunctionsHostAsync();
    }

    private static async Task WaitForFunctionsHostAsync(int timeoutSeconds = 60)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Functions host admin endpoint — available when host is ready
                var response = await http.GetAsync("http://localhost:7071/admin/host/status");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Functions host did not start within {timeoutSeconds}s. " +
            "Check that 'func' CLI is installed and EventHubDemo builds successfully.");
    }

    public async Task DisposeAsync()
    {
        _funcProcess?.Kill(entireProcessTree: true);
        _funcProcess?.Dispose();

        await ProducerClient.DisposeAsync();
        CosmosClient.Dispose();

        await Task.WhenAll(
            _azurite.DisposeAsync().AsTask(),
            _cosmos.DisposeAsync().AsTask(),
            _eventHubs.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(SensorPipelineFixture.Name)]
public class SensorPipelineGroup : ICollectionFixture<SensorPipelineFixture> { }

using Azure.Messaging.EventHubs.Producer;
using DotNet.Testcontainers.Builders;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.EventHubs;

namespace EventHubDemo.Tests;

public sealed class SensorPipelineFixture : IAsyncLifetime
{
    public const string Name = "SensorPipeline";

    // AzureWebJobsStorage for the Functions host (separate from the EventHubs emulator's internal Azurite).
    // Use latest image: Azure Functions Core Tools 4.8 requires Azurite API ≥ 2024-08-04 (available in 3.32+).
    private readonly AzuriteContainer _azurite = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
        .Build();

    // Port 8081 is bound to the same host port so the func child process can reach the emulator
    // via localhost:8081 (the self-referential endpoint the Cosmos SDK discovers from the account response).
    private readonly CosmosDbContainer _cosmos = new CosmosDbBuilder()
        .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
        .WithPortBinding(8081, 8081)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilMessageIsLogged("Gateway=OK, Explorer=OK"))
        .Build();

    private readonly EventHubsContainer _eventHubs;

    private Process? _funcProcess;

    public CosmosClient CosmosClient { get; private set; } = null!;
    public EventHubProducerClient ProducerClient { get; private set; } = null!;

    public SensorPipelineFixture()
    {
        _eventHubs = new EventHubsBuilder()
            .WithAcceptLicenseAgreement(true)
            .WithConfigurationBuilder(EventHubsServiceConfiguration.Create()
                .WithEntity("sensor-readings", 2, []))
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Emulator Service is Successfully Up!"))
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        await Task.WhenAll(
            _azurite.StartAsync(),
            _cosmos.StartAsync(),
            _eventHubs.StartAsync());

        var cosmosPort = _cosmos.GetMappedPublicPort(8081);
        var cosmosKey = _cosmos.GetConnectionString()
            .Split(';').First(p => p.StartsWith("AccountKey=", StringComparison.Ordinal))
            .Substring("AccountKey=".Length);

        CosmosClient = new CosmosClient(
            _cosmos.GetConnectionString(),
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => new System.Net.Http.HttpClient(new CosmosEmulatorHandler(cosmosPort)),
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
        var gitRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var functionAppDir = Path.Combine(gitRoot, "EventHubDemo");

        var env = new Dictionary<string, string?>
        {
            ["AzureWebJobsStorage"] = _azurite.GetConnectionString(),
            ["EventHubConnection"] = _eventHubs.GetConnectionString(),
            ["CosmosDbConnection"] = $"AccountEndpoint=http://localhost:{cosmosPort}/;AccountKey={cosmosKey}",  // vnext-preview: HTTP
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
        _funcProcess.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine("[func] " + e.Data); };
        _funcProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine("[func-err] " + e.Data); };
        _funcProcess.BeginOutputReadLine();
        _funcProcess.BeginErrorReadLine();

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

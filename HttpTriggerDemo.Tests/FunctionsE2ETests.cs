using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Testcontainers.Azurite;

namespace HttpTriggerDemo.Tests;

#pragma warning disable CA1001 // Type owns disposable fields — cleaned up in DisposeAsync
public class FunctionsE2ETests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();
    private Process? _funcProcess;
    private readonly HttpClient _client = new();

    public async Task InitializeAsync()
    {
        await _azurite.StartAsync();

        _funcProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "func",
            Arguments = "start --port 7071",
            WorkingDirectory = Path.GetFullPath("../../../../HttpTriggerDemo"),
            EnvironmentVariables =
            {
                ["AzureWebJobsStorage"] = _azurite.GetConnectionString(),
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated"
            },
            RedirectStandardOutput = true,
            UseShellExecute = false
        });

        await WaitForHostReady(_funcProcess!, TimeSpan.FromSeconds(30));
        await WaitForPortAsync("localhost", 7071, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_Returns201()
    {
        var response = await _client.PostAsJsonAsync(
            "http://localhost:7071/api/orders",
            new CreateOrderRequest("WIDGET-42", 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task WaitForHostReady(Process process, TimeSpan timeout)
    {
        var ready = new TaskCompletionSource<bool>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data?.Contains("For detailed output") == true)
                ready.TrySetResult(true);
        };
        process.BeginOutputReadLine();
        await ready.Task.WaitAsync(timeout);
    }

    private static async Task WaitForPortAsync(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(200);
            }
        }
        throw new TimeoutException($"Port {port} on {host} did not open within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        _funcProcess?.Kill(entireProcessTree: true);
        await _azurite.DisposeAsync();
        _client.Dispose();
    }
}

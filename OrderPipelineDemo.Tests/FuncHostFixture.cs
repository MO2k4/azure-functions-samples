using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Azure.Storage.Queues;

namespace OrderPipelineDemo.Tests;

// Runs the real Functions host (func start) against the shared Azurite container.
public sealed class FuncHostFixture(AzuriteFixture azurite) : IAsyncLifetime
{
    private Process? _func;

    public static bool CoreToolsInstalled { get; } =
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, OperatingSystem.IsWindows() ? "func.exe" : "func")));

    public async ValueTask InitializeAsync()
    {
        if (!CoreToolsInstalled)
            return;

        await new QueueClient(azurite.GetConnectionString(), "orders").CreateIfNotExistsAsync();

        var startInfo = new ProcessStartInfo("func", $"start --port {FreeTcpPort()}")
        {
            WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../OrderPipelineDemo")),
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.Environment["AzureWebJobsStorage"] = azurite.GetConnectionString();
        startInfo.Environment["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";

        var ready = new TaskCompletionSource();
        _func = Process.Start(startInfo)!;
        _func.OutputDataReceived += (_, e) =>
        {
            if (e.Data?.Contains("Worker process started and initialized", StringComparison.Ordinal) == true)
                ready.TrySetResult();
        };
        _func.BeginOutputReadLine();

        // func start builds the app first, so allow for a cold build.
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(90));
    }

    public ValueTask DisposeAsync()
    {
        _func?.Kill(entireProcessTree: true);
        _func?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

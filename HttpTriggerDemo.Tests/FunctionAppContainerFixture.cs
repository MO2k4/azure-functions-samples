using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace HttpTriggerDemo.Tests;

// Shared across all middleware test classes — one container per test session.
public sealed class FunctionAppContainerFixture : IAsyncLifetime
{
    public const string Name = "FunctionAppContainer";

    // Image name used both when building and when referencing in ContainerBuilder.
    private const string ImageName = "httptriggerdemo-test:latest";

    private IContainer _container = null!;
    private Uri _baseAddress = null!;

    public HttpClient CreateClient() =>
        new() { BaseAddress = _baseAddress };

    public async Task InitializeAsync()
    {
        // Build context must be the solution root so Directory.Build.props and
        // Directory.Packages.props are available to the Dockerfile.
        var gitRoot = CommonDirectoryPath.GetGitDirectory().DirectoryPath;

        // Build via Docker CLI rather than ImageFromDockerfileBuilder.
        // The Azure Functions base image has no ARM64 variant; the Dockerfile
        // uses FROM --platform=linux/amd64, which the CLI respects but the
        // Testcontainers build API does not (it pre-pulls without a platform).
        using var buildProcess = Process.Start(new ProcessStartInfo("docker",
            $"build -t {ImageName} -f HttpTriggerDemo/Dockerfile .")
        {
            WorkingDirectory = gitRoot,
            UseShellExecute = false,
        })!;

        await buildProcess.WaitForExitAsync();

        if (buildProcess.ExitCode != 0)
            throw new InvalidOperationException(
                $"docker build exited with code {buildProcess.ExitCode}. " +
                "Run: docker build -t httptriggerdemo-test -f HttpTriggerDemo/Dockerfile .");

        _container = new ContainerBuilder()
            .WithImage(ImageName)
            .WithPortBinding(80, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/api/hello")))
            .Build();

        await _container.StartAsync();
        _baseAddress = new Uri($"http://localhost:{_container.GetMappedPublicPort(80)}");
    }

    public async Task DisposeAsync() =>
        await _container.DisposeAsync().AsTask();
}

[CollectionDefinition(FunctionAppContainerFixture.Name)]
public class FunctionAppContainerGroup : ICollectionFixture<FunctionAppContainerFixture> { }

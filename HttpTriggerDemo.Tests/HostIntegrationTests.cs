using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace HttpTriggerDemo.Tests;

public class HostIntegrationTests : IAsyncLifetime
{
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                // WorkerHostedService opens a gRPC channel to the Functions host.
                // That host doesn't exist in tests — remove it or the build hangs.
                var worker = services.FirstOrDefault(s =>
                    s.ImplementationType?.Name == "WorkerHostedService");
                if (worker is not null)
                    services.Remove(worker);

                // Mirror the production DI registrations from Program.cs.
                // IOrderRepository: production impl is internal, so use a substitute.
                services.AddScoped<IOrderRepository>(_ => Substitute.For<IOrderRepository>());
                services.AddScoped<IOrderService, OrderService>();
            })
            .Build();

        await _host.StartAsync();
    }

    [Fact]
    public void IOrderService_ResolvesFromDi()
    {
        var service = _host.Services.GetService<IOrderService>();
        Assert.NotNull(service);
    }

    public async Task DisposeAsync() => await _host.StopAsync();
}

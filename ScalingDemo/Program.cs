using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScalingDemo;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register HttpClient as a singleton with pooled connection lifetime.
// PooledConnectionLifetime rotates DNS without creating new HttpClient instances,
// avoiding socket exhaustion that IHttpClientFactory solves differently.
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
})
{
    BaseAddress = new Uri("https://api.example.com")
});

// Heavy dependency: singleton with lazy initialization.
// The Lazy<T> wrapper defers construction until first use, keeping cold start fast
// for functions that don't need this service.
builder.Services.AddSingleton<Lazy<ExpensiveAnalyticsClient>>(sp =>
    new Lazy<ExpensiveAnalyticsClient>(() =>
    {
        var logger = sp.GetRequiredService<ILogger<ExpensiveAnalyticsClient>>();
        return new ExpensiveAnalyticsClient(logger);
    }));

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    LoggerFilterRule? defaultRule = options.Rules.FirstOrDefault(
        rule => rule.ProviderName ==
            "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (defaultRule is not null)
    {
        options.Rules.Remove(defaultRule);
    }
});

builder.Build().Run();

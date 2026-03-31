using HttpTriggerDemo;
using HttpTriggerDemo.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ExceptionHandlingMiddleware>(); // outermost — catches all
builder.UseMiddleware<CorrelationIdMiddleware>();     // innermost — per-request

builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// The SDK registers a default LoggerFilterRule that suppresses everything below Warning
// for the Application Insights provider. Remove it so Information-level logs flow through.
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

// Expose the implicit Program type so integration test projects can reference it
// via WebApplicationFactory<Program>.
public partial class Program { }

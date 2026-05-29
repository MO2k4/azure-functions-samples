using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderProcessor.Core.Configuration;
using OrderProcessor.Core.Services;

var builder = FunctionsApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry exporter, health checks, resilience.
// Under the Aspire AppHost this routes logs/traces/metrics to the dashboard;
// run standalone with `func start` it is a no-op (no OTLP endpoint configured).
builder.AddServiceDefaults();

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<OrderProcessingOptions>(
    builder.Configuration.GetSection("OrderProcessing"));

// AddOrderServices registers the OrderValidator and both keyed IOrderStore implementations
// (sql, cosmos) so functions can pick one with [FromKeyedServices(OrderStoreKeys.Sql)].
builder.Services.AddOrderServices();

builder.Build().Run();

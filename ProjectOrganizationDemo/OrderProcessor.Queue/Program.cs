using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using OrderProcessor.Core.Services;

var builder = FunctionsApplication.CreateBuilder(args);

// FunctionsApplication.CreateBuilder configures the worker host automatically.
// No ConfigureFunctionsWebApplication() needed because this app has no HTTP triggers.

// Aspire service defaults: OpenTelemetry exporter, health checks, resilience.
// Under the Aspire AppHost this routes logs/traces/metrics to the dashboard;
// run standalone with `func start` it is a no-op (no OTLP endpoint configured).
builder.AddServiceDefaults();

builder.Services.AddOrderServices();

builder.Build().Run();

using DaprAspireDemo.InventoryService.Stock;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and resilience, all from the shared project.
// This is the half of the picture Aspire owns: it is what puts this service's own spans in the
// dashboard. The Dapr sidecar's spans arrive by a separate route, configured in the AppHost.
builder.AddServiceDefaults();

var app = builder.Build();

// /health and /alive. The Dapr sidecar can be told to probe these with
// DaprSidecarOptions.EnableAppHealthCheck and AppHealthCheckPath.
app.MapDefaultEndpoints();

app.MapStockEndpoints();

await app.RunAsync();

using Dapr.Client;
using DaprAspireDemo.OrderService.Inventory;
using DaprAspireDemo.OrderService.Orders;

var builder = WebApplication.CreateBuilder(args);

// Aspire's half: OpenTelemetry, health checks, resilience. The OTLP endpoint, the resource
// attributes and the dashboard credentials all arrive as environment variables the AppHost
// injects, so nothing here names a collector.
builder.AddServiceDefaults();

// Dapr's half. AddDaprClient reads DAPR_HTTP_PORT and DAPR_GRPC_PORT from the environment, which
// the hosting integration sets on this process when it starts the sidecar, so 3500 and 50001 are
// never written down.
builder.Services.AddDaprClient();

// Service invocation runs over an ordinary HttpClient. CreateInvokeHttpClient sets BaseAddress to
// http://inventory-service and installs the handler that rewrites the request into
// {daprEndpoint}/v1.0/invoke/inventory-service/method/{path}. DaprClient.InvokeMethodAsync has
// been [Obsolete] since the Dapr .NET SDK 1.17, so this factory is the supported surface.
//
// It is registered as a keyed singleton rather than through AddHttpClient to keep the app ID out
// of reach of service discovery. AddServiceDefaults calls ConfigureHttpClientDefaults(http =>
// http.AddServiceDiscovery()), so every factory-created client in this process carries the Aspire
// service-discovery handler. That handler does no harm today: with no
// services__inventory-service__http__0 in configuration it falls through to the pass-through
// provider and leaves the URI alone, and an AddHttpClient registration with this BaseAddress and
// an InvocationHandler was measured working. It stops being harmless the moment such a value
// exists, say because someone adds WithReference(inventory) in the AppHost. Service discovery
// then rewrites the host to the resolved endpoint, and Dapr's InvocationHandler reads whatever
// host it is handed as the app ID:
//   {"errorCode":"ERR_DIRECT_INVOKE","message":"failed to invoke, id: localhost,
//    err: couldn't find service: localhost"}
// The two mechanisms are not exclusive, they are ordered, and service discovery wins.
//
// Traces survive the detour. AddHttpClientInstrumentation listens on the HttpClient
// DiagnosticSource, not on the factory, so the outbound span is recorded either way.
builder.Services.AddKeyedSingleton<HttpClient>(
    InventoryClient.AppId,
    (_, key) => DaprClient.CreateInvokeHttpClient(appId: (string)key!));

builder.Services.AddSingleton<InventoryClient>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapOrderEndpoints();

await app.RunAsync();

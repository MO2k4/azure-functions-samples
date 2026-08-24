using Dapr.Client;
using OrderApi;
using OrderApi.Inventory;
using OrderApi.Orders;

var builder = WebApplication.CreateBuilder(args);

// Source-generated JSON, front of the chain. The reflection resolver stays behind it because
// the framework serialises types this app never declares (ProblemDetails, most obviously).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OrderApiJsonContext.Default));

// The same generated metadata, handed to Dapr. Without this the client falls back to
// reflection-based serialisation for every state value and every invocation payload, which is
// the one place a trimmed or AOT-published service would fail at runtime rather than at build.
// AddDaprClient reads DAPR_HTTP_PORT / DAPR_GRPC_PORT from the environment, so 3500 and 50001
// are never written down here.
builder.Services.AddDaprClient(dapr =>
    dapr.UseJsonSerializationOptions(OrderApiJsonContext.Default.Options));

// Service invocation runs over an ordinary HttpClient. CreateInvokeHttpClient sets BaseAddress
// to http://inventory-api and installs the handler that rewrites the request into
// {daprEndpoint}/v1.0/invoke/inventory-api/method/{path}. One client per target app ID is the
// documented pattern (an app ID containing an uppercase letter only works when it is passed
// here), so the app ID doubles as the DI key.
//
// The instance form, daprClient.CreateInvokableHttpClient("inventory-api"), is equally current
// and delegates to this static one. Prefer it when the DaprClient it hangs off already carries
// a non-default endpoint or API token that would otherwise have to be repeated.
builder.Services.AddKeyedSingleton<HttpClient>(
    InventoryClient.AppId,
    (_, key) => DaprClient.CreateInvokeHttpClient(appId: (string)key!));

builder.Services.AddSingleton<InventoryClient>();

var app = builder.Build();

app.MapOrderEndpoints();

await app.RunAsync();

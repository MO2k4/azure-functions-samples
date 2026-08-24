using InventoryApi;
using InventoryApi.Stock;

var builder = WebApplication.CreateBuilder(args);

// The only wiring this service needs. No AddDaprClient, because it never calls out through a
// sidecar; it only gets called through one.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, InventoryApiJsonContext.Default));

var app = builder.Build();

app.MapStockEndpoints();

await app.RunAsync();

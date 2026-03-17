using EventHubDemo;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

var cosmosConnectionString = builder.Configuration["CosmosDbConnection"]
    ?? throw new InvalidOperationException("CosmosDbConnection is not configured");

builder.Services.AddSingleton(_ => new CosmosClient(
    cosmosConnectionString,
    new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    }));

builder.Services.AddScoped<ICosmosRepository, CosmosSensorRepository>();
builder.Services.AddScoped<ISensorProcessor, SensorProcessorService>();

builder.Build().Run();

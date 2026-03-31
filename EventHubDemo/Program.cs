using Azure.Monitor.OpenTelemetry.Exporter;
using EventHubDemo;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
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

var otel = builder.Services.AddOpenTelemetry();
otel.UseFunctionsWorkerDefaults();
otel.WithTracing(tp => tp.AddAzureMonitorTraceExporter())
    .WithMetrics(mp => mp.AddAzureMonitorMetricExporter());

builder.Build().Run();

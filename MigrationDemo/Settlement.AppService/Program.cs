using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Settlement.AppService.Configuration;
using Settlement.AppService.Services;
using Settlement.Core.Configuration;
using Settlement.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<SettlementOptions>()
    .Bind(builder.Configuration.GetSection(SettlementOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<PaymentSettlerOptions>()
    .Bind(builder.Configuration.GetSection(PaymentSettlerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<QueueOptions>()
    .Bind(builder.Configuration.GetSection(QueueOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register the storage clients via Microsoft.Extensions.Azure. ConnectionString
// is the local / Azurite path; ServiceUri + DefaultAzureCredential is the
// managed-identity path in Azure. Exactly one must be set.
var queueSection = builder.Configuration.GetSection(QueueOptions.SectionName);
var queueConnection = queueSection["ConnectionString"];
var queueServiceUri = queueSection["ServiceUri"];

builder.Services.AddAzureClients(clientBuilder =>
{
    if (!string.IsNullOrWhiteSpace(queueConnection))
    {
        clientBuilder.AddQueueServiceClient(queueConnection);
    }
    else if (!string.IsNullOrWhiteSpace(queueServiceUri))
    {
        clientBuilder.AddQueueServiceClient(new Uri(queueServiceUri));
        clientBuilder.UseCredential(new DefaultAzureCredential());
    }
    else
    {
        throw new InvalidOperationException(
            "Queue:ConnectionString or Queue:ServiceUri must be configured.");
    }
});

// QueueServiceClient is a per-account singleton; QueueClient is a per-queue
// view derived from it. Constructing it here keeps SettlementWorker's existing
// QueueClient dependency intact while routing through AddAzureClients.
builder.Services.AddSingleton(sp =>
{
    var service = sp.GetRequiredService<QueueServiceClient>();
    var name = sp.GetRequiredService<IOptions<QueueOptions>>().Value.QueueName;
    return service.GetQueueClient(name);
});

builder.Services.AddSingleton<ISettlementGateway, FakeSettlementGateway>();
builder.Services.AddSingleton<SettlementWorkerStatus>();
builder.Services.AddSettlementCore();
builder.Services.AddHostedService<SettlementWorker>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/status", (SettlementWorkerStatus status) => Results.Ok(status.Snapshot()));

app.Run();

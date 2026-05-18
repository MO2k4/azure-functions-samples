using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Settlement.ContainerApp;
using Settlement.ContainerApp.Configuration;
using Settlement.Core.Configuration;
using Settlement.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton(sp =>
{
    var service = sp.GetRequiredService<QueueServiceClient>();
    var name = sp.GetRequiredService<IOptions<QueueOptions>>().Value.QueueName;
    return service.GetQueueClient(name);
});

builder.Services.AddSingleton<ISettlementGateway, FakeSettlementGateway>();
builder.Services.AddSettlementCore();
builder.Services.AddHostedService<SettlementWorker>();

await builder.Build().RunAsync();

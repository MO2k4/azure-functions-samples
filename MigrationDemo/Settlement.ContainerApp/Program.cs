using Azure.Storage.Queues;
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
    .AddOptions<QueueOptions>()
    .Bind(builder.Configuration.GetSection(QueueOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<QueueOptions>>().Value;
    return new QueueClient(options.ConnectionString, options.QueueName);
});

builder.Services.AddSingleton<ISettlementGateway, FakeSettlementGateway>();
builder.Services.AddSettlementCore();
builder.Services.AddHostedService<SettlementWorker>();

await builder.Build().RunAsync();

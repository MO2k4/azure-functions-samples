using Azure.Storage.Queues;
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
    .AddOptions<QueueOptions>()
    .Bind(builder.Configuration.GetSection(QueueOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueueOptions>>().Value;
    return new QueueClient(options.ConnectionString, options.QueueName);
});

builder.Services.AddSingleton<ISettlementGateway, FakeSettlementGateway>();
builder.Services.AddSingleton<SettlementWorkerStatus>();
builder.Services.AddSettlementCore();
builder.Services.AddHostedService<SettlementWorker>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/status", (SettlementWorkerStatus status) => Results.Ok(status.Snapshot()));

app.Run();

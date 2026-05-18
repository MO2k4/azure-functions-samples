using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Settlement.Core.Configuration;
using Settlement.Core.Services;

var builder = FunctionsApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<ISettlementGateway, FakeSettlementGateway>();
builder.Services.AddSettlementCore();

builder.Build().Run();

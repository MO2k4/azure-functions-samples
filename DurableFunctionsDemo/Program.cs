using DurableFunctionsDemo;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// The isolated worker injects IPaymentGateway into PaymentActivities' constructor.
builder.Services.AddSingleton<IPaymentGateway, PaymentGateway>();

builder.Build().Run();

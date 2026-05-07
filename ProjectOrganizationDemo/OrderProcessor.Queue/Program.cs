using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using OrderProcessor.Core.Services;

var builder = FunctionsApplication.CreateBuilder(args);

// FunctionsApplication.CreateBuilder configures the worker host automatically.
// No ConfigureFunctionsWebApplication() needed because this app has no HTTP triggers.

builder.Services.AddOrderServices();

builder.Build().Run();

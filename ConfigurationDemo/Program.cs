using ConfigurationDemo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddOptions<ApiOptions>()
    .BindConfiguration("Api")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Build().Run();

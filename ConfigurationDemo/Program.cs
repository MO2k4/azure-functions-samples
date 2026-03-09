using ConfigurationDemo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddOptions<ApiOptions>()
    .BindConfiguration("Api")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddHttpClient("OrdersApi", (sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Build().Run();

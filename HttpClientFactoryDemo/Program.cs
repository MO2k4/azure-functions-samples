using HttpClientFactoryDemo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddOptions<PaymentsOptions>()
    .Bind(builder.Configuration.GetSection(PaymentsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Typed client: pooled handlers, sockets recycled on rotation, automatic disposal.
// Never `new HttpClient()` in a function — that path leaks sockets and
// holds DNS entries past their TTL.
builder.Services
    .AddHttpClient<IPaymentsApi, PaymentsApi>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<PaymentsOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddStandardResilienceHandler();

builder.Build().Run();

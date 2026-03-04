using HttpTriggerDemo.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ExceptionHandlingMiddleware>(); // outermost — catches all
builder.UseMiddleware<CorrelationIdMiddleware>();     // innermost — per-request

builder.Build().Run();

// Expose the implicit Program type so integration test projects can reference it
// via WebApplicationFactory<Program>.
public partial class Program { }

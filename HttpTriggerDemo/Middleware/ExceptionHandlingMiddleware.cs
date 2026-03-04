using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo.Middleware;

public class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in function {FunctionName}",
                context.FunctionDefinition.Name);

            var httpContext = context.GetHttpContext();
            if (httpContext is not null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                // Intentionally no body — do not leak exception details
            }
        }
    }
}

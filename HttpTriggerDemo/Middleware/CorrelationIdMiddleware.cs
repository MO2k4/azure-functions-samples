using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace HttpTriggerDemo.Middleware;

public class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext is null)
        {
            await next(context);
            return;
        }

        string correlationId = httpContext.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        httpContext.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}

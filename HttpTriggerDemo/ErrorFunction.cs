using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace HttpTriggerDemo;

public class ErrorFunction
{
    [Function("Error")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        throw new InvalidOperationException("Intentional error for middleware testing.");
    }
}

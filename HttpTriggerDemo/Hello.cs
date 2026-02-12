using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo;

public class HelloFunction(ILogger<HelloFunction> logger)
{
    [Function("Hello")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        logger.LogInformation("Hello function triggered");

        string? name = req.Query["name"];
        return new OkObjectResult($"Hello, {name ?? "world"}!");
    }
}

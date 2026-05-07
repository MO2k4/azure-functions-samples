using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions15;

public sealed class Endpoint10
{
    [Function("Endpoint10")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint10")] HttpRequest req)
        => new OkObjectResult(new { id = 10 });
}

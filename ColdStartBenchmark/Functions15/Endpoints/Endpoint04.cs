using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions15;

public sealed class Endpoint04
{
    [Function("Endpoint04")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint04")] HttpRequest req)
        => new OkObjectResult(new { id = 4 });
}

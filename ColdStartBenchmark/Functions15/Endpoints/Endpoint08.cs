using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions15;

public sealed class Endpoint08
{
    [Function("Endpoint08")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint08")] HttpRequest req)
        => new OkObjectResult(new { id = 8 });
}

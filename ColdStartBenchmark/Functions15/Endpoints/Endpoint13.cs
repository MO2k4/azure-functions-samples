using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions15;

public sealed class Endpoint13
{
    [Function("Endpoint13")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint13")] HttpRequest req)
        => new OkObjectResult(new { id = 13 });
}

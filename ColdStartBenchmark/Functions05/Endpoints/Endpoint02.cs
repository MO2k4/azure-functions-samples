using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions05;

public sealed class Endpoint02
{
    [Function("Endpoint02")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint02")] HttpRequest req)
        => new OkObjectResult(new { id = 2 });
}

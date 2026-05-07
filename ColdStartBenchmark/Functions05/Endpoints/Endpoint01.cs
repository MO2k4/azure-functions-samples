using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions05;

public sealed class Endpoint01
{
    [Function("Endpoint01")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint01")] HttpRequest req)
        => new OkObjectResult(new { id = 1 });
}

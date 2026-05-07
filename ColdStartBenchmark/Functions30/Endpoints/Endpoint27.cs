using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions30;

public sealed class Endpoint27
{
    [Function("Endpoint27")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint27")] HttpRequest req)
        => new OkObjectResult(new { id = 27 });
}

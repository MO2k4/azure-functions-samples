using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions15;

public sealed class Endpoint05
{
    [Function("Endpoint05")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint05")] HttpRequest req)
        => new OkObjectResult(new { id = 5 });
}

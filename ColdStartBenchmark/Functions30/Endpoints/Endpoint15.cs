using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions30;

public sealed class Endpoint15
{
    [Function("Endpoint15")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint15")] HttpRequest req)
        => new OkObjectResult(new { id = 15 });
}

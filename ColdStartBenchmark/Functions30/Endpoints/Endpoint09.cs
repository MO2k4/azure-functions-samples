using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions30;

public sealed class Endpoint09
{
    [Function("Endpoint09")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint09")] HttpRequest req)
        => new OkObjectResult(new { id = 9 });
}

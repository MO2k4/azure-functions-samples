using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions30;

public sealed class Endpoint17
{
    [Function("Endpoint17")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint17")] HttpRequest req)
        => new OkObjectResult(new { id = 17 });
}

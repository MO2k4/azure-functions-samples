using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Functions30;

public sealed class Endpoint25
{
    [Function("Endpoint25")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint25")] HttpRequest req)
        => new OkObjectResult(new { id = 25 });
}

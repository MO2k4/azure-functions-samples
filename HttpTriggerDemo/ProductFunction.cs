using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo;

public class ProductFunction(ILogger<ProductFunction> logger)
{
    [Function("GetProduct")]
    public IActionResult GetProduct(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "products/{category:alpha}/{id:int?}")] HttpRequest req,
        string category, int? id)
    {
        logger.LogInformation("Looking up {Category}, id={Id}", category, id);
        return new OkObjectResult(new { category, id });
    }
}

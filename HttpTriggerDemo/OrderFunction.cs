using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace HttpTriggerDemo;

public record CreateOrderRequest(string ProductId, int Quantity);

public class OrderFunction(ILogger<OrderFunction> logger)
{
    [Function("CreateOrder")]
    public IActionResult CreateOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest order)
    {
        logger.LogInformation("Order for {ProductId} x{Quantity}", order.ProductId, order.Quantity);
        return new CreatedResult($"/orders/{Guid.NewGuid()}", order);
    }
}

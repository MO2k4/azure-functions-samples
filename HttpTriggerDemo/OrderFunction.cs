using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace HttpTriggerDemo;

public record CreateOrderRequest(string ProductId, int Quantity);

public class OrderFunction(IOrderService orderService, ILogger<OrderFunction> logger)
{
    [Function("CreateOrder")]
    public async Task<IActionResult> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest order)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ProductId"] = order.ProductId,
            ["Quantity"] = order.Quantity
        });

        var result = await orderService.CreateOrderAsync(order);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(result.Error);

        return new CreatedResult($"/orders/{result.Order!.OrderId}", result.Order);
    }
}

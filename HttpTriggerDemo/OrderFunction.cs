using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace HttpTriggerDemo;

public record CreateOrderRequest(string ProductId, int Quantity);

public class OrderFunction(IOrderService orderService)
{
    [Function("CreateOrder")]
    public async Task<IActionResult> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest order)
    {
        var result = await orderService.CreateOrderAsync(order);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(result.Error);

        return new CreatedResult($"/orders/{result.Order!.OrderId}", result.Order);
    }
}

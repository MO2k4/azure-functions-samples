using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace OrdersApi;

// AuthorizationLevel.Function: callers without the function key get a 401 from the
// Functions host, so API Management (which sends the key) is the only way in.
public sealed class OrderFunctions(OrderStore store, ILogger<OrderFunctions> logger)
{
    [Function("list-orders")]
    public IActionResult List(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")] HttpRequest req)
    {
        string? status = req.Query["status"];
        if (status is null)
            return new OkObjectResult(store.List(status: null));

        return Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed)
            ? new OkObjectResult(store.List(parsed))
            : new BadRequestObjectResult(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["status"] = [$"Unknown status '{status}'."] }));
    }

    [Function("get-order")]
    public IActionResult Get(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderId}")] HttpRequest req,
        string orderId) =>
        store.Find(orderId) is { } order
            ? new OkObjectResult(order)
            : new NotFoundResult();

    [Function("create-order")]
    public IActionResult Create(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest request)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));

        var order = store.Create(request);
        logger.LogInformation("Order {OrderId} placed by {CustomerId}, total {Total}",
            order.OrderId, order.CustomerId, order.Total);

        return new CreatedResult($"orders/{order.OrderId}", order);
    }
}

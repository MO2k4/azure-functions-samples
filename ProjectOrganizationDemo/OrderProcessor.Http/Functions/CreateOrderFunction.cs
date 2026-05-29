using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;
using OrderProcessor.Core.Stores;
using OrderProcessor.Core.Validators;
using OrderProcessor.Http.Models;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace OrderProcessor.Http.Functions;

// Primary-constructor function class with two keyed IOrderStore parameters.
// The HTTP app writes through the Sql store; Cosmos is registered for read failover.
public sealed class CreateOrderFunction(
    ILogger<CreateOrderFunction> logger,
    OrderValidator validator,
    [FromKeyedServices(OrderStoreKeys.Sql)] IOrderStore primaryStore)
{
    [Function(nameof(CreateOrder))]
    public async Task<CreateOrderOutput> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Create order {OrderId}", request.OrderId);

        var order = new Order(request.OrderId, request.CustomerId, request.Amount, OrderStatus.Pending);

        var validation = validator.Validate(order);
        if (!validation.IsValid)
        {
            return new CreateOrderOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = validation.Error }),
            };
        }

        await primaryStore.SaveAsync(order, cancellationToken);

        return new CreateOrderOutput
        {
            HttpResponse = new CreatedResult($"/api/orders/{order.OrderId}", order),
            OrderMessage = new OrderMessage(order.OrderId, order.CustomerId, order.Amount),
        };
    }
}

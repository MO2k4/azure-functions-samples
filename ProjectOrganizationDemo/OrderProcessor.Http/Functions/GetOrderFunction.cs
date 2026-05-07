using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;
using OrderProcessor.Core.Stores;

namespace OrderProcessor.Http.Functions;

// Reads from the Cosmos store to demonstrate that two functions in the same app
// can resolve different keyed implementations of the same interface.
public sealed class GetOrderFunction(
    ILogger<GetOrderFunction> logger,
    [FromKeyedServices(OrderStoreKeys.Cosmos)] IOrderStore readStore)
{
    [Function(nameof(GetOrder))]
    public async Task<IActionResult> GetOrder(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderId}")] HttpRequest req,
        string orderId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Get order {OrderId}", orderId);

        Order? order = await readStore.GetAsync(orderId, cancellationToken);
        return order is null ? new NotFoundResult() : new OkObjectResult(order);
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ScalingDemo;

public class OrdersFunction
{
    private readonly ILogger<OrdersFunction> _logger;
    private readonly HttpClient _httpClient;
    private readonly Lazy<ExpensiveAnalyticsClient> _analytics;

    public OrdersFunction(
        ILogger<OrdersFunction> logger,
        HttpClient httpClient,
        Lazy<ExpensiveAnalyticsClient> analytics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _analytics = analytics;
    }

    [Function("GetOrderCount")]
    public async Task<IActionResult> GetOrderCount(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/count")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting order count");

        var count = await _analytics.Value.GetOrderCountAsync(cancellationToken);

        return new OkObjectResult(new { count });
    }
}

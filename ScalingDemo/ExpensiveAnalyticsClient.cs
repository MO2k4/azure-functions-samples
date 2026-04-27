using Microsoft.Extensions.Logging;

namespace ScalingDemo;

/// <summary>
/// Simulates a heavy dependency that takes time to initialize:
/// loading ML models, opening persistent connections, building caches.
/// Registered as Lazy&lt;T&gt; so the construction cost only hits
/// when a function actually needs it.
/// </summary>
public sealed class ExpensiveAnalyticsClient
{
    private readonly ILogger<ExpensiveAnalyticsClient> _logger;

    public ExpensiveAnalyticsClient(ILogger<ExpensiveAnalyticsClient> logger)
    {
        _logger = logger;
        _logger.LogInformation("ExpensiveAnalyticsClient initialized");
    }

    public Task<int> GetOrderCountAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying analytics engine");
        return Task.FromResult(42);
    }
}

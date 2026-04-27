using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ScalingDemo;

public class Warmup
{
    private readonly ILogger<Warmup> _logger;
    private readonly HttpClient _httpClient;
    private readonly Lazy<ExpensiveAnalyticsClient> _analytics;

    public Warmup(
        ILogger<Warmup> logger,
        HttpClient httpClient,
        Lazy<ExpensiveAnalyticsClient> analytics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _analytics = analytics;
    }

    /// <summary>
    /// Fires when a new instance is added during scale-out (Premium and Flex Consumption only).
    /// Not available on the Consumption plan. Only fires during scale-out, not restarts.
    /// Use this to force-initialize lazy dependencies and warm up HTTP connection pools
    /// before the instance receives real traffic.
    /// </summary>
    [Function("Warmup")]
    public void Run([WarmupTrigger] object warmupContext)
    {
        // Force the Lazy<T> to construct now, during warmup, rather than
        // on the first real request. This shifts the initialization cost
        // to the prewarming window.
        _ = _analytics.Value;

        // Open at least one connection in the pool so the first real HTTP call
        // doesn't pay the TCP + TLS handshake cost.
        _ = _httpClient.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead);

        _logger.LogInformation(
            "Warmup complete: analytics client initialized, HTTP pool primed");
    }
}

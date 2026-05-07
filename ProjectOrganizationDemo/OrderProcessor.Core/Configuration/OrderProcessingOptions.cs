namespace OrderProcessor.Core.Configuration;

public sealed class OrderProcessingOptions
{
    public int MaxRetries { get; init; } = 3;
    public int BatchSize { get; init; } = 50;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

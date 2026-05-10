using System.ComponentModel.DataAnnotations;

namespace Settlement.AppService.Configuration;

public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string QueueName { get; init; } = "settlement-batches";

    [Range(1, 32)]
    public int MaxBatchMessages { get; init; } = 8;

    [Range(1, 600)]
    public int VisibilityTimeoutSeconds { get; init; } = 300;

    [Range(100, 60_000)]
    public int IdlePollingDelayMs { get; init; } = 5_000;
}

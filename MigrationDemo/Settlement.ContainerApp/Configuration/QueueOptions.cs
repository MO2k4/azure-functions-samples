using System.ComponentModel.DataAnnotations;

namespace Settlement.ContainerApp.Configuration;

public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    // Exactly one of ConnectionString (local / Azurite) or ServiceUri (managed
    // identity / DefaultAzureCredential) must be set. Validated at registration
    // time in Program.cs since data annotations can't express "one of".
    public string? ConnectionString { get; init; }

    public string? ServiceUri { get; init; }

    [Required]
    public string QueueName { get; init; } = "settlement-batches";

    [Range(1, 32)]
    public int MaxBatchMessages { get; init; } = 8;

    [Range(1, 3600)]
    public int VisibilityTimeoutSeconds { get; init; } = 1_800;

    [Range(100, 60_000)]
    public int IdlePollingDelayMs { get; init; } = 5_000;
}

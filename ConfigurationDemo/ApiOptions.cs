using System.ComponentModel.DataAnnotations;

namespace ConfigurationDemo;

public class ApiOptions
{
    [Required]
    public required string BaseUrl { get; init; }

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
}

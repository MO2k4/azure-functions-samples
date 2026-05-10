using System.ComponentModel.DataAnnotations;

namespace HttpClientFactoryDemo;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    [Required]
    [Url]
    public string BaseAddress { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

namespace Invoicing.Api.Models;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; init; } = 100;
    public int WindowSeconds { get; init; } = 1;
    public int QueueLimit { get; init; } = 10;
}

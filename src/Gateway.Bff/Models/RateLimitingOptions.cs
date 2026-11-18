namespace Gateway.Bff.Models;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; init; } = 300;
    public int WindowSeconds { get; init; } = 10;
    public int QueueLimit { get; init; } = 10;
}

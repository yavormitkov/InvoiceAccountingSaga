namespace Invoicing.Api.Models;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string PublicKey { get; init; } = string.Empty;
    public string KeyId { get; init; } = "cub-key-2026";
    public string ValidIssuer { get; init; } = "http://localhost:5000";
    public string ValidAudience { get; init; } = "Invoicing.Api";
    public int ClockSkewMinutes { get; init; } = 5;
}

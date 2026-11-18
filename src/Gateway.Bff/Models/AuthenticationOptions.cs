namespace Gateway.Bff.Models;

public sealed class AuthenticationOptions
{
    public bool DemoModeEnabled { get; set; }
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int TokenExpirationMinutes { get; set; }
}
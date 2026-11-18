namespace Financial.Api.Models;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public required string Host { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}

namespace Portal.BlazorUi.DTOs;

public sealed record InvoiceDetailsResponse(
        string InvoiceNumber,
        decimal Amount,
        DateTime RegisteredAt);
namespace Invoicing.Api.DTOs;

public sealed record InvoiceDetailsResponse(
    string InvoiceNumber, 
    decimal Amount, 
    DateTime RegisteredAt
);

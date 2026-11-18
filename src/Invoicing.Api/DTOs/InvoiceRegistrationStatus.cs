namespace Invoicing.Api.DTOs;

public enum InvoiceRegistrationStatus
{
    Success = 0,
    BailiffNotFound = 1,
    DuplicateInvoice = 2,
    DatabaseError = 3
}

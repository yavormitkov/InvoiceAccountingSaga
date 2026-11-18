namespace Invoicing.Api.DTOs
{
    public record CreateInvoiceRequest(string InvoiceNumber, string BailiffLicenseNumber, decimal Amount);
}
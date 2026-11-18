using MediatR;
using Invoicing.Api.DTOs;

namespace Invoicing.Api.Commands;

public sealed record RegisterInvoiceCommand(
    string InvoiceNumber,
    string BailiffLicenseNumber,
    decimal Amount
) : IRequest<RegisterInvoiceResult>;

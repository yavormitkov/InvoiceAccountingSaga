using System;

namespace Invoicing.Api.DTOs;

public record DatabaseInvoiceResult(Guid InvoiceId, Guid BailiffId, InvoiceRegistrationStatus ErrorCode);

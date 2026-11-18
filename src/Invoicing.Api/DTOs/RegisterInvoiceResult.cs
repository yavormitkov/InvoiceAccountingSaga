using System;

namespace Invoicing.Api.DTOs;

public record RegisterInvoiceResult(Guid InvoiceId, bool IsNew, InvoiceRegistrationStatus Status);

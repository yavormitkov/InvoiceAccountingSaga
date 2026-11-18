using Invoicing.Api.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Invoicing.Api.Queries;

public sealed record GetInvoicesByLicenseQuery(string LicenseNumber) : IRequest<IEnumerable<InvoiceDetailsResponse>>;

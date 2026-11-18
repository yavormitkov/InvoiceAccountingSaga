using System;
using MediatR;

namespace Financial.Api.Commands;

public sealed record ProcessInvoiceAccountingCommand(
    Guid InvoiceId,
    Guid BailiffId,
    decimal Amount
) : IRequest;

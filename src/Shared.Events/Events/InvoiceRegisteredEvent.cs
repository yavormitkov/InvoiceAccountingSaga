using System;

namespace Shared.Events.Events;

public sealed record InvoiceRegisteredEvent(
    Guid InvoiceId,
    Guid BailiffId,
    decimal Amount
);

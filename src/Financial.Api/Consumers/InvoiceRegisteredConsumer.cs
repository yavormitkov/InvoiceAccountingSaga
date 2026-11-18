using Financial.Api.Commands;
using MassTransit;
using MediatR;
using Shared.Events.Events;

namespace Financial.Api.Consumers;

public sealed class InvoiceRegisteredConsumer(IMediator mediator) : IConsumer<InvoiceRegisteredEvent>
{
    private readonly IMediator _mediator = mediator;

    public async Task Consume(ConsumeContext<InvoiceRegisteredEvent> context)
    {
        var msg = context.Message;
        await _mediator.Send(
            new ProcessInvoiceAccountingCommand(msg.InvoiceId, msg.BailiffId, msg.Amount),
            context.CancellationToken);
    }
}

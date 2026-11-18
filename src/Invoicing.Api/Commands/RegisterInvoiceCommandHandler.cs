using System.Data;
using Dapper;
using Invoicing.Api.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace Invoicing.Api.Commands;

public sealed class RegisterInvoiceCommandHandler(
    IDbConnection db,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<RegisterInvoiceCommandHandler> logger)
    : IRequestHandler<RegisterInvoiceCommand, RegisterInvoiceResult>
{
    private readonly IDbConnection _db = db;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider = pipelineProvider;
    private readonly ILogger<RegisterInvoiceCommandHandler> _logger = logger;

    public async Task<RegisterInvoiceResult> Handle(RegisterInvoiceCommand request, CancellationToken cancellationToken)
    {
        var prospectiveInvoiceId = Guid.NewGuid();
        var outboxEventId = Guid.NewGuid();
        var pipeline = _pipelineProvider.GetPipeline("db-policy");

        var dbResult = await pipeline.ExecuteAsync(async ct =>
        {
            var command = new CommandDefinition(
                SqlStatements.RegisterInvoiceSql,
                new
                {
                    InvoiceNumber = request.InvoiceNumber,
                    LicenseNumber = request.BailiffLicenseNumber,
                    Amount = request.Amount,
                    InvoiceId = prospectiveInvoiceId,
                    OutboxEventId = outboxEventId
                },
                cancellationToken: ct);

            return await _db.QuerySingleAsync<DatabaseInvoiceResult>(command);
        }, cancellationToken);

        if (dbResult.ErrorCode == InvoiceRegistrationStatus.Success)
        {
            return new RegisterInvoiceResult(dbResult.InvoiceId, true, InvoiceRegistrationStatus.Success);
        }

        if (dbResult.ErrorCode == InvoiceRegistrationStatus.DatabaseError)
        {
            _logger.LogError(
                "Critical Database Failure inside CQRS Command Handler. SQL Server CATCH block triggered for Invoice Number: {InvoiceNumber}.",
                request.InvoiceNumber);
        }

        return new RegisterInvoiceResult(dbResult.InvoiceId, false, dbResult.ErrorCode);
    }
}

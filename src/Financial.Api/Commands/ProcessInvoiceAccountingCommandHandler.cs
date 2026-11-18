using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Registry;

namespace Financial.Api.Commands;

public sealed class ProcessInvoiceAccountingCommandHandler : IRequestHandler<ProcessInvoiceAccountingCommand>
{
    private readonly string _connectionString;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    private const string IdempotentUpsertSql = @"
        SET XACT_ABORT ON;
        SET NOCOUNT ON;

        BEGIN TRANSACTION;
        BEGIN TRY
            IF EXISTS (SELECT 1 FROM dbo.ProcessedInvoices WITH (UPDLOCK, HOLDLOCK) WHERE InvoiceId = @InvoiceId)
            BEGIN
                COMMIT TRANSACTION;
                RETURN;
            END;

            INSERT INTO dbo.ProcessedInvoices (InvoiceId, ProcessedAt)
            VALUES (@InvoiceId, SYSUTCDATETIME());

            IF EXISTS (SELECT 1 FROM dbo.UserBalances WITH (UPDLOCK, HOLDLOCK) WHERE UserIdentifier = @UserIdentifier)
            BEGIN
                UPDATE dbo.UserBalances 
                SET Balance = Balance + @Amount,
                    InvoiceCount = InvoiceCount + 1
                WHERE UserIdentifier = @UserIdentifier;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.UserBalances (Id, UserIdentifier, Balance, InvoiceCount)
                VALUES (@Id, @UserIdentifier, @Amount, 1);
            END;

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";

    public ProcessInvoiceAccountingCommandHandler(
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _connectionString = configuration.GetConnectionString("FinancialDb")
            ?? throw new InvalidOperationException("FinancialDb connection string is missing.");
        _pipelineProvider = pipelineProvider;
    }

    public async Task Handle(ProcessInvoiceAccountingCommand request, CancellationToken cancellationToken)
    {
        var pipeline = _pipelineProvider.GetPipeline("db-fallback-policy");

        await pipeline.ExecuteAsync(async ct =>
        {
            await using var connection = new SqlConnection(_connectionString);

            var command = new CommandDefinition(
                IdempotentUpsertSql,
                new
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = request.InvoiceId,
                    UserIdentifier = request.BailiffId,
                    Amount = request.Amount
                },
                cancellationToken: ct);

            await connection.ExecuteAsync(command).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}

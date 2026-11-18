using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Dapper;
using Polly;
using Polly.Registry;
using Financial.Api.Commands;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Financial.Api.Commands.Handlers;

public sealed class ProcessInvoiceAccountingCommandHandler : IRequestHandler<ProcessInvoiceAccountingCommand>
{
    private readonly string _connectionString;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    private const string UpsertSql = @"
        BEGIN TRANSACTION;
        BEGIN TRY
            IF EXISTS (SELECT 1 FROM UserBalances WITH (XLOCK, ROWLOCK) WHERE UserIdentifier = @UserIdentifier)
            BEGIN
                UPDATE UserBalances 
                SET Balance = Balance + @Amount,
                    InvoiceCount = InvoiceCount + 1
                WHERE UserIdentifier = @UserIdentifier;
            END
            ELSE
            BEGIN
                INSERT INTO UserBalances (Id, UserIdentifier, Balance, InvoiceCount)
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
            using var connection = new SqlConnection(_connectionString);

            await connection.ExecuteAsync(UpsertSql, new
            {
                Id = Guid.NewGuid(),
                UserIdentifier = request.BailiffId.ToString(),
                Amount = request.Amount
            }).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}

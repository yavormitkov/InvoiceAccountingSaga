using System.Data;
using System.Text.Json;
using Dapper;
using Invoicing.Api.Commands;
using MassTransit;
using Microsoft.Data.SqlClient;
using Shared.Events.Events;

namespace Invoicing.Api.Workers;

public sealed class OutboxRelayWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<OutboxRelayWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                int processedCount;
                do
                {
                    processedCount = await DispatchBatchAsync(stoppingToken);
                }
                while (processedCount == BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transient error while polling OutboxEvents. Will retry on next tick.");
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbConnection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        if (dbConnection is not SqlConnection sqlConn)
        {
            throw new InvalidOperationException("Expected SqlConnection for OutboxRelayWorker.");
        }

        await using (sqlConn)
        {
            if (sqlConn.State != ConnectionState.Open)
            {
                await sqlConn.OpenAsync(cancellationToken);
            }

            await using var transaction = await sqlConn.BeginTransactionAsync(cancellationToken);

            var pending = (await sqlConn.QueryAsync<OutboxRow>(
                new CommandDefinition(
                    SqlStatements.FetchPendingOutboxEventsSql,
                    transaction: transaction,
                    cancellationToken: cancellationToken))).ToList();

            if (pending.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            foreach (var row in pending)
            {
                var domainEvent = JsonSerializer.Deserialize<InvoiceRegisteredEvent>(row.Payload);
                if (domainEvent is null)
                {
                    _logger.LogError("Corrupt Outbox payload detected for OutboxId {OutboxId}.", row.Id);
                    continue;
                }

                await publishEndpoint.Publish(domainEvent, ctx =>
                {
                    ctx.MessageId = row.Id;
                }, cancellationToken);

                await sqlConn.ExecuteAsync(
                    new CommandDefinition(
                        SqlStatements.MarkOutboxEventProcessedSql,
                        new { Id = row.Id },
                        transaction: transaction,
                        cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return pending.Count;
        }
    }

    private sealed record OutboxRow(Guid Id, string EventType, string Payload);
}

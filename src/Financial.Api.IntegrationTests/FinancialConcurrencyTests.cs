using Dapper;
using Financial.Api.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Financial.Api.IntegrationTests;

public sealed class FinancialConcurrencyTests : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FinancialConcurrencyTests()
    {
        var saPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "CubSecurePass2026!";
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FinancialDb")
                            ?? $"Server=localhost,1433;Database=FinancialDb;User Id=sa;Password={saPassword};TrustServerCertificate=True;";

        Environment.SetEnvironmentVariable("RabbitMQ__Host", Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost");
        Environment.SetEnvironmentVariable("RabbitMQ__Username", Environment.GetEnvironmentVariable("RabbitMQ__Username") ?? "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest");
        Environment.SetEnvironmentVariable("ConnectionStrings__FinancialDb", _connectionString);
    }

    [Fact]
    public async Task ProcessInvoiceAccounting_ShouldHandleHighConcurrency_AndPreventDuplicateRedelivery()
    {
        var testBailiffId = Guid.NewGuid();
        var testLicense = $"TEST-{Guid.NewGuid():N}"[..15];
        const int concurrentTasksCount = 10;
        const decimal amountPerInvoice = 100.00m;
        var invoiceIds = Enumerable.Range(0, concurrentTasksCount).Select(_ => Guid.NewGuid()).ToList();

        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(
            "INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) VALUES (@Id, @License, @Name);",
            new { Id = testBailiffId, License = testLicense, Name = "Integration Test Bailiff" });

        try
        {
            // 1. Dispatch 10 distinct concurrent invoices
            var tasks = invoiceIds.Select(invoiceId => Task.Run(async () =>
            {
                using var scope = Server.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ProcessInvoiceAccountingCommand(invoiceId, testBailiffId, amountPerInvoice));
            }));

            await Task.WhenAll(tasks);

            // 2. Simulate duplicate at-least-once RabbitMQ redelivery of the first invoice
            using (var scope = Server.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ProcessInvoiceAccountingCommand(invoiceIds[0], testBailiffId, amountPerInvoice));
            }

            var finalState = await conn.QuerySingleAsync<(int InvoiceCount, decimal Balance)>(
                "SELECT InvoiceCount, Balance FROM dbo.UserBalances WHERE UserIdentifier = @Id;",
                new { Id = testBailiffId });

            Assert.Equal(concurrentTasksCount, finalState.InvoiceCount);
            Assert.Equal(concurrentTasksCount * amountPerInvoice, finalState.Balance);
        }
        finally
        {
            await conn.ExecuteAsync("DELETE FROM dbo.ProcessedInvoices WHERE InvoiceId IN @Ids;", new { Ids = invoiceIds });
            await conn.ExecuteAsync("DELETE FROM dbo.UserBalances WHERE UserIdentifier = @Id;", new { Id = testBailiffId });
            await conn.ExecuteAsync("DELETE FROM dbo.Bailiffs WHERE Id = @Id;", new { Id = testBailiffId });
        }
    }
}

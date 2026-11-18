using Dapper;
using Invoicing.Api.DTOs;
using MediatR;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Invoicing.Api.Queries;

public sealed class GetInvoicesByLicenseHandler(IDbConnection dbConnection)
    : IRequestHandler<GetInvoicesByLicenseQuery, IEnumerable<InvoiceDetailsResponse>>
{
    private readonly IDbConnection _dbConnection = dbConnection;

    public async Task<IEnumerable<InvoiceDetailsResponse>> Handle(GetInvoicesByLicenseQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT i.InvoiceNumber, i.Amount, i.RegisteredAt 
            FROM dbo.Invoices i
            INNER JOIN dbo.Bailiffs b ON i.BailiffId = b.Id
            WHERE b.LicenseNumber = @LicenseNumber
            ORDER BY i.RegisteredAt DESC;";

        var command = new CommandDefinition(
            sql,
            new { LicenseNumber = request.LicenseNumber },
            cancellationToken: cancellationToken);

        return await _dbConnection.QueryAsync<InvoiceDetailsResponse>(command);
    }
}

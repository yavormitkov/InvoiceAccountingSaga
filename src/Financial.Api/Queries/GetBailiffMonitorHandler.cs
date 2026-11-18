using Financial.Api.Data;
using Financial.Api.DTOs;
using Financial.Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Financial.Api.Queries;

public sealed class GetBailiffMonitorHandler(IDbContextFactory<FinancialDbContext> contextFactory)
    : IRequestHandler<GetBailiffMonitorQuery, IEnumerable<BailiffMonitorResponse>>
{
    private readonly IDbContextFactory<FinancialDbContext> _contextFactory = contextFactory;

    public async Task<IEnumerable<BailiffMonitorResponse>> Handle(GetBailiffMonitorQuery request, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var items = await context.Set<UserBalance>()
            .AsNoTracking()
            .Join(
                context.Set<Bailiff>().AsNoTracking(),
                ub => ub.UserIdentifier,
                b => b.Id,
                (ub, b) => new
                {
                    b.LicenseNumber,
                    b.Name,
                    ub.InvoiceCount,
                    ub.Balance
                }
            )
            .OrderBy(x => x.LicenseNumber)
            .ToListAsync(cancellationToken);

        return items.Select(x => new BailiffMonitorResponse(
            x.LicenseNumber,
            x.Name,
            x.InvoiceCount,
            x.Balance
        ));
    }
}

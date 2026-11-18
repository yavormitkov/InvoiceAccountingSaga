using Financial.Api.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Financial.Api.Extensions;

public static class FinancialEndpointExtensions
{
    public static IEndpointRouteBuilder MapFinancialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var config = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        bool isDemoMode = config.GetValue<bool>("Authentication:DemoModeEnabled", true);

        var getMonitor = endpoints.MapGet("/api/financial/bailiffs", async (IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetBailiffMonitorQuery(), cancellationToken);
            return Results.Ok(result);
        });

        if (!isDemoMode)
        {
            getMonitor.RequireAuthorization();
        }

        return endpoints;
    }
}

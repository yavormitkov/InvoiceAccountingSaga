using Financial.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Financial.Api.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddCustomDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FinancialDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Critical Infrastructure Gap: FinancialDb connection string is missing!");
        }

        services.AddDbContextFactory<FinancialDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}

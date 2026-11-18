using System.Data;
using Microsoft.Data.SqlClient;

namespace Invoicing.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddCustomDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IDbConnection>(sp =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__InvoicingDb")
                                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = configuration.GetConnectionString("InvoicingDb")
                                   ?? configuration.GetConnectionString("DefaultConnection");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Critical Infrastructure Gap: Database connection string is completely missing from container environment!");
            }

            return new SqlConnection(connectionString);
        });

        return services;
    }
}

using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace Invoicing.Api.Extensions;

public static class ResilienceExtensions
{
    public static IServiceCollection AddDatabaseResilience(this IServiceCollection services)
    {
        return services.AddResiliencePipeline("db-policy", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>()
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(50)
            });
        });
    }
}

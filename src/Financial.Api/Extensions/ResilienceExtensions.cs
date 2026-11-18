using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace Financial.Api.Extensions;

public static class ResilienceExtensions
{
    public static IServiceCollection AddDatabaseResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline("db-fallback-policy", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>()
                    .Handle<System.TimeoutException>()
            });
        });

        return services;
    }
}

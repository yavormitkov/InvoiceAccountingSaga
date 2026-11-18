using Financial.Api.Consumers;
using MassTransit;

namespace Financial.Api.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddFinancialMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["RabbitMQ:Host"];
        var username = configuration["RabbitMQ:Username"];
        var password = configuration["RabbitMQ:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Critical Infrastructure Gap: RabbitMQ keys are missing from Financial configuration!");
        }

        services.AddMassTransit(x =>
        {
            x.AddConsumer<InvoiceRegisteredConsumer>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromMilliseconds(100),
                    maxInterval: TimeSpan.FromSeconds(2),
                    intervalDelta: TimeSpan.FromMilliseconds(200)));
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

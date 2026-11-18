using Invoicing.Api.Models;
using MassTransit;

namespace Invoicing.Api.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddInvoicingMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>();

        if (rabbitOptions is null ||
            string.IsNullOrWhiteSpace(rabbitOptions.Host) ||
            string.IsNullOrWhiteSpace(rabbitOptions.Username) ||
            string.IsNullOrWhiteSpace(rabbitOptions.Password))
        {
            throw new InvalidOperationException("Critical Infrastructure Gap: RabbitMQ keys are missing from environment configuration!");
        }

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitOptions.Host, "/", h =>
                {
                    h.Username(rabbitOptions.Username);
                    h.Password(rabbitOptions.Password);
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

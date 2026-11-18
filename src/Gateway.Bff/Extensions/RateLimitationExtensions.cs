using System.Threading.RateLimiting;
using Gateway.Bff.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Gateway.Bff.Extensions;

public static class RateLimitationExtensions
{
    public const string BotPolicyName = "BotRateLimiterPolicy";

    public static IServiceCollection AddEdgeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                          ?? new RateLimitingOptions();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddSlidingWindowLimiter(BotPolicyName, fixedOptions =>
            {
                fixedOptions.PermitLimit = rateOptions.PermitLimit;
                fixedOptions.Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds);
                fixedOptions.SegmentsPerWindow = 10;
                fixedOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                fixedOptions.QueueLimit = rateOptions.QueueLimit;
            });
        });
    }
}

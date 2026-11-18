using System.Threading.RateLimiting;
using Invoicing.Api.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Invoicing.Api.Extensions;

public static class RateLimitationExtensions
{
    public const string BotPolicyName = "banco-bot-policy";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                          ?? new RateLimitingOptions();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddSlidingWindowLimiter(BotPolicyName, opt =>
            {
                opt.PermitLimit = rateOptions.PermitLimit;
                opt.Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds);
                opt.SegmentsPerWindow = 10;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = rateOptions.QueueLimit;
            });
        });
    }
}

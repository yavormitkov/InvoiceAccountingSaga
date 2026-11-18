using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text.Json;
using Invoicing.Api.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Invoicing.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddHostedService<OutboxRelayWorker>();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddDatabaseResilience();
builder.Services.AddCustomDatabase(builder.Configuration);
builder.Services.AddInvoicingMassTransit(builder.Configuration);
builder.Services.AddCustomAuthentication(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (exception is BadHttpRequestException || exception is JsonException || exception?.InnerException is JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            logger.LogWarning("Malformed JSON payload received at edge boundary: {Message}", exception.Message);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            logger.LogError(exception, "Unhandled infrastructure or application exception.");
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(string.Empty);
    });
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapInvoiceEndpoints();

app.Run();

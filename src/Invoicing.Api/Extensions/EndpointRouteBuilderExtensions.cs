using Invoicing.Api.Commands;
using Invoicing.Api.Data;
using Invoicing.Api.DTOs;
using Invoicing.Api.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Invoicing.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices");

        group.MapPost("/", async (
            [FromBody] CreateInvoiceRequest request,
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.InvoiceNumber) ||
                string.IsNullOrWhiteSpace(request.BailiffLicenseNumber) ||
                request.Amount <= 0)
            {
                return Results.BadRequest(new { Message = "Invalid invoice details or negative amount." });
            }

            var command = new RegisterInvoiceCommand(
                request.InvoiceNumber,
                request.BailiffLicenseNumber,
                request.Amount);

            var response = await mediator.Send(command, cancellationToken);

            return response.Status switch
            {
                InvoiceRegistrationStatus.Success => Results.Created($"/api/invoices/{response.InvoiceId}", new
                {
                    InvoiceId = response.InvoiceId,
                    Status = "Created"
                }),
                InvoiceRegistrationStatus.DuplicateInvoice => Results.Ok(new
                {
                    InvoiceId = response.InvoiceId,
                    Status = "AlreadyRegistered"
                }),
                InvoiceRegistrationStatus.BailiffNotFound => Results.NotFound(new
                {
                    Message = $"Bailiff with license '{request.BailiffLicenseNumber}' not found."
                }),
                InvoiceRegistrationStatus.DatabaseError => Results.StatusCode(StatusCodes.Status500InternalServerError),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization(AuthorizationConstants.InvoiceWritePolicy)
        .RequireRateLimiting(RateLimitationExtensions.BotPolicyName);

        group.MapGet("/bailiff/{licenseNumber}", async (
            [FromRoute] string licenseNumber,
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(licenseNumber))
            {
                return Results.BadRequest(new { Message = "License number cannot be empty." });
            }

            var result = await mediator.Send(new GetInvoicesByLicenseQuery(licenseNumber), cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous();
    }
}

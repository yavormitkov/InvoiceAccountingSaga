using System.Security.Cryptography;
using Invoicing.Api.Data;
using Invoicing.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Invoicing.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>();

        if (options is null || string.IsNullOrWhiteSpace(options.PublicKey))
        {
            throw new InvalidOperationException("Critical Security Gap: 'Authentication:PublicKey' is missing in container configuration!");
        }

        var rsa = RSA.Create();
        var trimmedKey = options.PublicKey.Trim();

        if (trimmedKey.StartsWith("-----BEGIN"))
        {
            rsa.ImportFromPem(trimmedKey);
        }
        else
        {
            var keyBytes = Convert.FromBase64String(trimmedKey);
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        }

        var rsaKey = new RsaSecurityKey(rsa) { KeyId = options.KeyId };

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.ValidIssuer,
                    ValidateAudience = true,
                    ValidAudience = options.ValidAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = rsaKey,
                    ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                };
            });

        services.AddAuthorization(auth =>
        {
            auth.AddPolicy(AuthorizationConstants.InvoiceWritePolicy, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("scope", AuthorizationConstants.InvoicesCreateScope));
        });

        return services;
    }
}

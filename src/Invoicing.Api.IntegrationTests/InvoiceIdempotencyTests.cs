using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Dapper;
using Invoicing.Api.Data;
using Invoicing.Api.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Invoicing.Api.IntegrationTests;

public sealed class InvoiceIdempotencyTests : WebApplicationFactory<Program>
{
    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly RSA _rsa;

    public InvoiceIdempotencyTests()
    {
        var saPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "CubSecurePass2026!";
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__InvoicingDb")
                            ?? $"Server=localhost,1433;Database=CubConnectDb;User Id=sa;Password={saPassword};TrustServerCertificate=True;";

        _rsa = RSA.Create(2048);
        var ephemeralPublicKey = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());

        Environment.SetEnvironmentVariable("RabbitMQ__Host", Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost");
        Environment.SetEnvironmentVariable("RabbitMQ__Username", Environment.GetEnvironmentVariable("RabbitMQ__Username") ?? "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest");
        Environment.SetEnvironmentVariable("Authentication__ValidIssuer", "http://localhost:5000");
        Environment.SetEnvironmentVariable("Authentication__ValidAudience", "Invoicing.Api");
        Environment.SetEnvironmentVariable("Authentication__KeyId", "cub-key-2026");
        Environment.SetEnvironmentVariable("Authentication__PublicKey", ephemeralPublicKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__InvoicingDb", _connectionString);

        _client = CreateClient();
    }

    [Fact]
    public async Task RegisterInvoice_ShouldEnforceIdempotencyAndSingleOutboxEvent_UnderConcurrentCollisions()
    {
        var testBailiffId = Guid.NewGuid();
        var testLicense = $"TEST-{Guid.NewGuid():N}"[..15];
        var uniqueInvoiceNumber = $"INV-RACE-{Guid.NewGuid():N}";
        var payload = new CreateInvoiceRequest(uniqueInvoiceNumber, testLicense, 1250.50m);
        var token = GenerateSignedJwt(_rsa, AuthorizationConstants.InvoicesCreateScope);

        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) VALUES (@Id, @License, @Name);",
            new { Id = testBailiffId, License = testLicense, Name = "Race Test Bailiff" });

        try
        {
            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "api/invoices")
                {
                    Content = JsonContent.Create(payload)
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                await startGate.Task;
                return await _client.SendAsync(req);
            })).ToArray();

            startGate.SetResult();
            var responses = await Task.WhenAll(tasks);

            var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created], statusCodes);

            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<InvoiceCreatedResponse>()));
            Assert.All(bodies, b => Assert.NotNull(b));
            Assert.Equal(bodies[0]!.InvoiceId, bodies[1]!.InvoiceId);

            var outboxCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.OutboxEvents WHERE JSON_VALUE(Payload, '$.InvoiceId') = @InvoiceId;",
                new { InvoiceId = bodies[0]!.InvoiceId.ToString() });

            Assert.Equal(1, outboxCount);
        }
        finally
        {
            await conn.ExecuteAsync(
                "DELETE FROM dbo.OutboxEvents WHERE JSON_VALUE(Payload, '$.BailiffId') = @BailiffId;",
                new { BailiffId = testBailiffId.ToString() });
            await conn.ExecuteAsync("DELETE FROM dbo.Invoices WHERE BailiffId = @BailiffId;", new { BailiffId = testBailiffId });
            await conn.ExecuteAsync("DELETE FROM dbo.Bailiffs WHERE Id = @Id;", new { Id = testBailiffId });
        }
    }

    [Fact]
    public async Task RegisterInvoice_ShouldRejectForgedOrUnauthorizedTokens()
    {
        var payload = new CreateInvoiceRequest("INV-UNAUTH-001", "B-001", 100.00m);

        // 1. Token signed with an untrusted RSA key -> 401 Unauthorized
        using var rogueRsa = RSA.Create(2048);
        var forgedToken = GenerateSignedJwt(rogueRsa, AuthorizationConstants.InvoicesCreateScope);

        using var forgedReq = new HttpRequestMessage(HttpMethod.Post, "api/invoices") { Content = JsonContent.Create(payload) };
        forgedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forgedToken);
        var forgedRes = await _client.SendAsync(forgedReq);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedRes.StatusCode);

        // 2. Valid signature, missing required scope -> 403 Forbidden
        var wrongScopeToken = GenerateSignedJwt(_rsa, "invoices:read");
        using var forbiddenReq = new HttpRequestMessage(HttpMethod.Post, "api/invoices") { Content = JsonContent.Create(payload) };
        forbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongScopeToken);
        var forbiddenRes = await _client.SendAsync(forbiddenReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);
    }

    private static string GenerateSignedJwt(RSA signingRsa, string scope)
    {
        var key = new RsaSecurityKey(signingRsa) { KeyId = "cub-key-2026" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "http://localhost:5000",
            audience: "Invoicing.Api",
            claims: [new Claim("scope", scope)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record InvoiceCreatedResponse(Guid InvoiceId, string Status);
}

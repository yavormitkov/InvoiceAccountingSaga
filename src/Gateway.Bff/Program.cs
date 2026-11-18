using Gateway.Bff.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEdgeRateLimiting(builder.Configuration);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy();

await app.RunAsync();

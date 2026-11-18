using Financial.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddCustomDatabase(builder.Configuration);
builder.Services.AddFinancialMassTransit(builder.Configuration);
builder.Services.AddDatabaseResilience();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

app.UseAuthorization();
app.MapFinancialEndpoints();

await app.RunAsync();

namespace Financial.Api.DTOs;

public sealed record BailiffMonitorResponse(
    string LicenseNumber,
    string Name,
    int TotalInvoices,
    decimal CurrentBalance
);

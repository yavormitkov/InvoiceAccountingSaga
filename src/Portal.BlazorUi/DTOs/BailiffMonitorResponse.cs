namespace Portal.BlazorUi.DTOs;

public sealed record BailiffMonitorResponse(
        string LicenseNumber,
        string Name,
        int TotalInvoices,
        decimal CurrentBalance);

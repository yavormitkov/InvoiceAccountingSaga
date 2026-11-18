using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Portal.BlazorUi.DTOs;

namespace Portal.BlazorUi.Pages;

public partial class HomeBase : ComponentBase
{
    [Inject]
    protected HttpClient Http { get; set; } = default!;

    protected List<BailiffMonitorResponse>? Bailiffs { get; set; }
    protected List<InvoiceDetailsResponse>? Invoices { get; set; }
    protected string? SelectedLicense { get; set; }
    protected string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Bailiffs = await FetchDataAsync<List<BailiffMonitorResponse>>(
            "api/financial/bailiffs",
            "Unable to load bailiff balances. Please verify that Financial.Api is reachable.");
    }

    protected async Task SelectBailiffAsync(string licenseNumber)
    {
        SelectedLicense = licenseNumber;
        Invoices = null;

        Invoices = await FetchDataAsync<List<InvoiceDetailsResponse>>(
            $"api/invoices/bailiff/{Uri.EscapeDataString(licenseNumber)}",
            $"Unable to load invoices for license {licenseNumber}.");
    }

    protected void ClearSelection()
    {
        SelectedLicense = null;
        Invoices = null;
        ErrorMessage = null;
    }

    private async Task<T?> FetchDataAsync<T>(string requestUri, string fallbackErrorMessage)
    {
        try
        {
            ErrorMessage = null;
            return await Http.GetFromJsonAsync<T>(requestUri);
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"{fallbackErrorMessage} (HTTP {(int?)ex.StatusCode ?? 503})";
            return default;
        }
        catch (Exception)
        {
            ErrorMessage = fallbackErrorMessage;
            return default;
        }
    }
}

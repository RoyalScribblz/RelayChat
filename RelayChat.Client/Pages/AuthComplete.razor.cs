using Microsoft.AspNetCore.Components;
using RelayChat.Client.Services;

namespace RelayChat.Client.Pages;

public partial class AuthComplete : ComponentBase
{
    [Inject]
    public required AuthService AuthService { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    protected string? _errorMessage { get; private set; }
    protected string _loginUrl => AuthService.GetLoginUrl(ReturnUrl);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await AuthService.Exchange();
            NavigationManager.NavigateTo(string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl, forceLoad: true);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }
}

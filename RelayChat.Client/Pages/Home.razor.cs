using Microsoft.AspNetCore.Components;
using RelayChat.Client.Services;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    public required AuthService AuthService { get; init; }

    [Inject]
    public required ControlPlaneApiClient ControlPlaneApiClient { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required NodeApiClient NodeApiClient { get; init; }

    protected bool _isAuthenticated { get; private set; }
    protected string _nodeName { get; private set; } = "Relay";
    protected MembershipDto? _membership { get; private set; }
    protected IReadOnlyList<ChannelDto> _channels { get; private set; } = [];
    protected string? _errorMessage { get; private set; }
    protected string? _profileStatusMessage { get; private set; }
    protected string? _profileErrorMessage { get; private set; }
    protected string _profileName { get; set; } = string.Empty;
    protected string _profileHandle { get; set; } = string.Empty;
    protected string? _profileAvatarUrl { get; set; }
    protected string? _profileEmail { get; set; }
    protected string _profilePreviewHandle => _profileHandle.Trim().TrimStart('@');

    protected override async Task OnInitializedAsync()
    {
        await AuthService.Initialize();
        await LoadData();
    }

    protected void Login()
    {
        NavigationManager.NavigateTo(
            AuthService.GetLoginUrl(NavigationManager.ToBaseRelativePath(NavigationManager.Uri)),
            forceLoad: true);
    }

    protected async Task JoinNode()
    {
        if (!_isAuthenticated)
        {
            Login();
            return;
        }

        _membership = await NodeApiClient.JoinNode();
    }

    protected async Task SaveProfile()
    {
        if (!_isAuthenticated)
        {
            Login();
            return;
        }

        _profileStatusMessage = null;
        _profileErrorMessage = null;

        try
        {
            var profile = await ControlPlaneApiClient.UpdateCurrentUser(new UpdateProfileRequest(
                _profileName,
                _profileHandle,
                _profileAvatarUrl));

            if (profile is null)
            {
                return;
            }

            ApplyProfile(profile);
            await AuthService.ApplyProfile(profile);
            _profileStatusMessage = "Profile updated.";
        }
        catch (Exception ex)
        {
            _profileErrorMessage = ex.Message;
        }
    }

    protected string GetChannelHref(Guid channelId)
    {
        return $"/channels/{channelId}";
    }

    private async Task LoadData()
    {
        try
        {
            _isAuthenticated = AuthService.IsAuthenticated;
            var node = await NodeApiClient.GetNode();
            _nodeName = node?.Name ?? "Relay";
            _channels = await NodeApiClient.GetChannels();
            _membership = _isAuthenticated ? await NodeApiClient.GetMembership() : null;

            if (_isAuthenticated)
            {
                var profile = await ControlPlaneApiClient.GetCurrentUser();
                if (profile is not null)
                {
                    ApplyProfile(profile);
                    await AuthService.ApplyProfile(profile);
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private void ApplyProfile(UserProfileDto profile)
    {
        _profileName = profile.Name;
        _profileHandle = profile.Handle;
        _profileAvatarUrl = profile.AvatarUrl;
        _profileEmail = profile.Email;
    }
}

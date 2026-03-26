using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Pages;

public partial class Home : ComponentBase, IDisposable
{
    private readonly Dictionary<Guid, List<ChannelDto>> channelsByServer = [];
    private readonly Dictionary<Guid, ServerMembershipDto?> membershipsByServer = [];
    private HttpClient? httpClient;
    private string newServerName = string.Empty;

    [Parameter]
    public Guid? ServerId { get; set; }

    [Inject]
    public required NodeApiOptions NodeApiOptions { get; init; }

    [Inject]
    public required Services.ChatClient ChatClient { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    protected IReadOnlyList<ServerDto> _servers { get; private set; } = [];
    protected IReadOnlyDictionary<Guid, List<ChannelDto>> _channelsByServer => channelsByServer;
    protected ServerDto? _selectedServer { get; private set; }
    protected IReadOnlyList<ChannelDto> _selectedChannels =>
        _selectedServer is not null && channelsByServer.TryGetValue(_selectedServer.Id, out var channels)
            ? channels
            : [];
    protected bool _isSelectedServerMember =>
        _selectedServer is not null &&
        membershipsByServer.TryGetValue(_selectedServer.Id, out var membership) &&
        membership is not null;
    protected ServerMembershipRole? _selectedMembershipRole =>
        _selectedServer is not null &&
        membershipsByServer.TryGetValue(_selectedServer.Id, out var membership) &&
        membership is not null
            ? membership.Role
            : null;
    protected string _newServerName
    {
        get => newServerName;
        set => newServerName = value;
    }
    protected string? _errorMessage { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        httpClient = new HttpClient { BaseAddress = new Uri(NodeApiOptions.BaseUrl) };
        await LoadData();
    }

    protected override Task OnParametersSetAsync()
    {
        SelectServer();
        return Task.CompletedTask;
    }

    private async Task LoadData()
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        try
        {
            var servers = await httpClient.GetFromJsonAsync<List<ServerDto>>("/servers") ?? [];
            _servers = servers;

            channelsByServer.Clear();
            membershipsByServer.Clear();
            foreach (var server in servers)
            {
                var channels = await httpClient.GetFromJsonAsync<List<ChannelDto>>($"/servers/{server.Id}/channels") ?? [];
                channelsByServer[server.Id] = channels;
                membershipsByServer[server.Id] = await GetMembership(server.Id);
            }

            SelectServer();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    public void Dispose()
    {
        httpClient?.Dispose();
    }

    protected string GetChannelHref(Guid serverId, Guid channelId)
    {
        return $"/servers/{serverId}/channels/{channelId}";
    }

    protected string GetServerHref(Guid serverId)
    {
        return $"/servers/{serverId}";
    }

    protected async Task CreateServer()
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var request = new CreateServerRequest(newServerName.Trim(), ChatClient.UserId);
        var result = await (await httpClient.PostAsJsonAsync("/servers", request)).Content
            .ReadFromJsonAsync<CreateServerResultDto>();
        if (result is null)
        {
            return;
        }

        newServerName = string.Empty;
        NavigationManager.NavigateTo($"/servers/{result.Server.Id}/channels/{result.Channel.Id}");
    }

    protected async Task JoinSelectedServer()
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (_selectedServer is null)
        {
            return;
        }

        await httpClient.PostAsJsonAsync($"/servers/{_selectedServer.Id}/memberships", new JoinServerRequest(ChatClient.UserId));
        await LoadData();
        StateHasChanged();
    }

    private void SelectServer()
    {
        _selectedServer = ServerId.HasValue
            ? _servers.FirstOrDefault(server => server.Id == ServerId.Value)
            : _servers.FirstOrDefault();
    }

    private async Task<ServerMembershipDto?> GetMembership(Guid serverId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var response = await httpClient.GetAsync($"/servers/{serverId}/memberships/{ChatClient.UserId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServerMembershipDto>();
    }
}

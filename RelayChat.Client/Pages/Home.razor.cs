using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace RelayChat.Client.Pages;

public partial class Home : ComponentBase, IDisposable
{
    private readonly Dictionary<Guid, List<ChannelDto>> channelsByServer = [];
    private HttpClient? httpClient;

    [Parameter]
    public Guid? ServerId { get; set; }

    [Inject]
    public required NodeApiOptions NodeApiOptions { get; init; }

    protected IReadOnlyList<ServerDto> _servers { get; private set; } = [];
    protected IReadOnlyDictionary<Guid, List<ChannelDto>> _channelsByServer => channelsByServer;
    protected ServerDto? _selectedServer { get; private set; }
    protected IReadOnlyList<ChannelDto> _selectedChannels =>
        _selectedServer is not null && channelsByServer.TryGetValue(_selectedServer.Id, out var channels)
            ? channels
            : [];
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
            foreach (var server in servers)
            {
                var channels = await httpClient.GetFromJsonAsync<List<ChannelDto>>($"/servers/{server.Id}/channels") ?? [];
                channelsByServer[server.Id] = channels;
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

    private void SelectServer()
    {
        _selectedServer = ServerId.HasValue
            ? _servers.FirstOrDefault(server => server.Id == ServerId.Value)
            : _servers.FirstOrDefault();
    }
}

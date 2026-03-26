using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace RelayChat.Client.Pages;

public partial class Home : ComponentBase, IDisposable
{
    private readonly Dictionary<Guid, List<ChannelDto>> channelsByServer = [];
    private HttpClient? httpClient;

    [Inject]
    public required NodeApiOptions NodeApiOptions { get; init; }

    protected IReadOnlyList<ServerDto> _servers { get; private set; } = [];
    protected IReadOnlyDictionary<Guid, List<ChannelDto>> _channelsByServer => channelsByServer;
    protected string? _errorMessage { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        httpClient = new HttpClient { BaseAddress = new Uri(NodeApiOptions.BaseUrl) };

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
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Services;

public sealed class NodeApiClient(AuthService authService, NodeApiOptions options)
{
    public async Task<NodeDto?> GetNode(CancellationToken ct = default)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<NodeDto>("/node", ct);
    }

    public async Task<List<ChannelDto>> GetChannels(CancellationToken ct = default)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<ChannelDto>>("/channels", ct) ?? [];
    }

    public async Task<MembershipDto?> GetMembership(CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        using var response = await client.GetAsync("/memberships/me", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MembershipDto>(ct);
    }

    public async Task<MembershipDto?> JoinNode(CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        using var response = await client.PostAsync("/memberships", null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MembershipDto>(ct);
    }

    public async Task<List<MembershipDto>> GetMembers(CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        return await client.GetFromJsonAsync<List<MembershipDto>>("/memberships", ct) ?? [];
    }

    public async Task<VoiceChannelStateDto?> GetVoiceChannelState(Guid channelId, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        return await client.GetFromJsonAsync<VoiceChannelStateDto>($"/voice/channels/{channelId}", ct);
    }

    public async Task<VoiceChannelAccessDto?> GetVoiceChannelAccess(Guid channelId, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        using var response = await client.PostAsync($"/voice/channels/{channelId}/access", null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VoiceChannelAccessDto>(ct);
    }

    public async Task<List<MessageDto>> GetMessages(Guid channelId, Guid? before = null, Guid? after = null, int? limit = null, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        var query = new List<string>();
        if (before.HasValue)
        {
            query.Add($"before={before.Value}");
        }

        if (after.HasValue)
        {
            query.Add($"after={after.Value}");
        }

        if (limit.HasValue)
        {
            query.Add($"limit={limit.Value}");
        }

        var path = $"/channels/{channelId}/messages";
        if (query.Count > 0)
        {
            path = $"{path}?{string.Join("&", query)}";
        }

        return await client.GetFromJsonAsync<List<MessageDto>>(path, ct) ?? [];
    }

    public async Task<ChannelDto?> CreateChannel(CreateChannelRequest request, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        using var response = await client.PostAsJsonAsync("/channels", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChannelDto>(ct);
    }

    public async Task<List<ChannelDto>> ReorderChannels(IReadOnlyList<Guid> channelIds, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetNodeToken());

        using var response = await client.PutAsJsonAsync("/channels/order", new ReorderChannelsRequest(channelIds), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChannelDto>>(ct) ?? [];
    }

    private HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl)
        };
    }
}

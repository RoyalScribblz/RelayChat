using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public sealed class VoicePresenceService(
    VoiceSessionRepository repository,
    IHubContext<ChatHub> hubContext)
{
    public Task<List<VoiceParticipantDto>> GetParticipants(Guid channelId, CancellationToken ct = default)
    {
        return GetParticipantsInternal(channelId, ct);
    }

    public async Task Join(Guid channelId, ClaimsPrincipal user, string connectionId, CancellationToken ct = default)
    {
        var userId = user.GetRequiredUserId();
        var previous = await repository.Get(userId, ct);
        var previousChannelId = previous?.ChannelId;

        await repository.Upsert(new VoiceSession
        {
            UserId = userId,
            ChannelId = channelId,
            ConnectionId = connectionId,
            Name = user.GetDisplayName(),
            Handle = user.GetHandle(),
            AvatarUrl = user.GetAvatarUrl(),
            IsMuted = false,
            JoinedAt = previous?.JoinedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, ct);

        if (previousChannelId.HasValue && previousChannelId.Value != channelId)
        {
            await Broadcast(previousChannelId.Value, ct);
        }

        await Broadcast(channelId, ct);
    }

    public async Task Leave(Guid userId, string? connectionId = null, CancellationToken ct = default)
    {
        var existing = await repository.Get(userId, ct);
        if (existing is null)
        {
            return;
        }

        if (connectionId is not null && !string.Equals(existing.ConnectionId, connectionId, StringComparison.Ordinal))
        {
            return;
        }

        var channelId = existing.ChannelId;
        await repository.Remove(userId, ct);
        await Broadcast(channelId, ct);
    }

    public async Task SetMuted(Guid userId, bool isMuted, CancellationToken ct = default)
    {
        var existing = await repository.Get(userId, ct);
        if (existing is null)
        {
            return;
        }

        await repository.UpdateMute(userId, isMuted, ct);
        await Broadcast(existing.ChannelId, ct);
    }

    public async Task RefreshProfile(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var channelId = await repository.SyncProfile(
            user.GetRequiredUserId(),
            user.GetDisplayName(),
            user.GetHandle(),
            user.GetAvatarUrl(),
            ct);

        if (channelId.HasValue)
        {
            await Broadcast(channelId.Value, ct);
        }
    }

    private async Task Broadcast(Guid channelId, CancellationToken ct)
    {
        var state = new VoiceChannelStateDto(channelId, await GetParticipantsInternal(channelId, ct));
        await hubContext.Clients.All.SendAsync("ReceiveVoiceChannelState", state, ct);
    }

    private async Task<List<VoiceParticipantDto>> GetParticipantsInternal(Guid channelId, CancellationToken ct)
    {
        var sessions = await repository.GetByChannel(channelId, ct);
        return sessions
            .Select(session => new VoiceParticipantDto(
                session.UserId,
                session.ChannelId,
                session.Name,
                session.Handle,
                session.AvatarUrl,
                session.IsMuted))
            .ToList();
    }
}

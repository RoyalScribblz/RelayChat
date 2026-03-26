using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

[Authorize]
public sealed class ChatHub(
    ChannelRepository channelRepository,
    MessageRepository messageRepository,
    MembershipRepository membershipRepository,
    VoicePresenceService voicePresenceService) : Hub
{
    public async Task JoinChannel(JoinChannelRequest request)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null)
        {
            return;
        }

        var membership = await membershipRepository.Get(user.GetRequiredUserId());
        if (membership is null)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, request.ChannelId.ToString());
    }

    public async Task JoinVoiceChannel(Guid channelId)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        var channel = await channelRepository.Get(channelId);
        if (channel is null || channel.Type != ChannelType.Voice)
        {
            return;
        }

        var membership = await membershipRepository.Get(user.GetRequiredUserId());
        if (membership is null)
        {
            return;
        }

        await voicePresenceService.Join(channelId, user, Context.ConnectionId);
    }

    public async Task LeaveVoiceChannel()
    {
        var user = Context.User;
        if (user is null)
        {
            return;
        }

        await voicePresenceService.Leave(user.GetRequiredUserId(), Context.ConnectionId);
    }

    public async Task SetVoiceMuted(bool isMuted)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        await voicePresenceService.SetMuted(user.GetRequiredUserId(), isMuted);
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null || channel.Type != ChannelType.Text)
        {
            return;
        }

        var userId = user.GetRequiredUserId();
        var membership = await membershipRepository.Get(userId);
        if (membership is null || membership.Role is not MembershipRole.Admin and not MembershipRole.Member)
        {
            return;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = request.ChannelId,
            AuthorId = userId,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            ClientMessageId = request.ClientMessageId
        };

        await messageRepository.Add(message);
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessage", message.ToDto());
    }

    public async Task EditMessage(EditMessageRequest request)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null || channel.Type != ChannelType.Text)
        {
            return;
        }

        var userId = user.GetRequiredUserId();
        var membership = await membershipRepository.Get(userId);
        var message = await messageRepository.Get(request.ChannelId, request.MessageId);
        if (message is null ||
            membership is null ||
            membership.Role is not MembershipRole.Admin and not MembershipRole.Member ||
            message.AuthorId != userId ||
            message.DeletedAt.HasValue)
        {
            return;
        }

        message.Content = request.Content;
        message.EditedAt = DateTimeOffset.UtcNow;

        await messageRepository.SaveChanges();
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessageUpdated", message.ToDto());
    }

    public async Task DeleteMessage(DeleteMessageRequest request)
    {
        var user = Context.User;
        if (user is null || !user.HasNodeAccess())
        {
            return;
        }

        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null || channel.Type != ChannelType.Text)
        {
            return;
        }

        var userId = user.GetRequiredUserId();
        var membership = await membershipRepository.Get(userId);
        var message = await messageRepository.Get(request.ChannelId, request.MessageId);
        if (message is null || membership is null || message.DeletedAt.HasValue)
        {
            return;
        }

        var canDelete = message.AuthorId == userId || membership.Role == MembershipRole.Admin;
        if (!canDelete)
        {
            return;
        }

        message.DeletedAt = DateTimeOffset.UtcNow;

        await messageRepository.SaveChanges();
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessageUpdated", message.ToDto());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = Context.User;
        if (user is not null)
        {
            await voicePresenceService.Leave(user.GetRequiredUserId(), Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

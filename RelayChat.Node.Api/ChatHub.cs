using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

[Authorize]
public sealed class ChatHub(
    ChannelRepository channelRepository,
    MessageRepository messageRepository,
    MembershipRepository membershipRepository) : Hub
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

    public async Task SendMessage(SendMessageRequest request)
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
        if (channel is null)
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
        if (channel is null)
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
}

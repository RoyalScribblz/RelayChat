using Microsoft.AspNetCore.SignalR;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public sealed class ChatHub(
    ChannelRepository channelRepository,
    MessageRepository messageRepository,
    ServerMembershipRepository membershipRepository) : Hub
{
    public async Task JoinChannel(JoinChannelRequest request)
    {
        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null)
        {
            return;
        }

        var membership = await membershipRepository.Get(channel.ServerId, request.AuthorId);
        if (membership is null)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, request.ChannelId.ToString());
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null)
        {
            return;
        }

        var membership = await membershipRepository.Get(channel.ServerId, request.AuthorId);
        if (membership is null || membership.Role is not ServerMembershipRole.Admin and not ServerMembershipRole.Member)
        {
            return;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = request.ChannelId,
            AuthorId = request.AuthorId,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            ClientMessageId = request.ClientMessageId
        };

        await messageRepository.Add(message);
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessage", message.ToDto());
    }

    public async Task EditMessage(EditMessageRequest request)
    {
        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null)
        {
            return;
        }

        var membership = await membershipRepository.Get(channel.ServerId, request.AuthorId);
        var message = await messageRepository.Get(request.ChannelId, request.MessageId);
        if (message is null ||
            membership is null ||
            membership.Role is not ServerMembershipRole.Admin and not ServerMembershipRole.Member ||
            message.AuthorId != request.AuthorId ||
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
        var channel = await channelRepository.Get(request.ChannelId);
        if (channel is null)
        {
            return;
        }

        var membership = await membershipRepository.Get(channel.ServerId, request.AuthorId);
        var message = await messageRepository.Get(request.ChannelId, request.MessageId);
        if (message is null || membership is null || message.DeletedAt.HasValue)
        {
            return;
        }

        var canDelete = message.AuthorId == request.AuthorId || membership.Role == ServerMembershipRole.Admin;
        if (!canDelete)
        {
            return;
        }

        message.DeletedAt = DateTimeOffset.UtcNow;

        await messageRepository.SaveChanges();
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessageUpdated", message.ToDto());
    }
}

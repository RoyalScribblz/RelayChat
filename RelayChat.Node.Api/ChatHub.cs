using Microsoft.AspNetCore.SignalR;

namespace RelayChat.Node.Api;

public sealed class ChatHub(MessageRepository repository) : Hub
{
    public Task JoinChannel(JoinChannelRequest request)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, request.ChannelId.ToString());
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = request.ChannelId,
            AuthorId = request.AuthorId,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            ClientMessageId = request.ClientMessageId
        };

        await repository.Add(message);
        await Clients.Group(request.ChannelId.ToString()).SendAsync("ReceiveMessage", MessageDto.FromMessage(message));
    }
}

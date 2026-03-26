namespace RelayChat.Node.Contracts;

public sealed record SendMessageRequest(
    Guid ChannelId,
    string Content,
    Guid ClientMessageId);

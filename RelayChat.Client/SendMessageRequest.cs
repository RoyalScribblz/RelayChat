namespace RelayChat.Client;

public sealed record SendMessageRequest(
    Guid ChannelId,
    Guid AuthorId,
    string Content,
    Guid ClientMessageId);

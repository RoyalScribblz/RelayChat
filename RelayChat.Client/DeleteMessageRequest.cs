namespace RelayChat.Client;

public sealed record DeleteMessageRequest(
    Guid ChannelId,
    Guid MessageId,
    Guid AuthorId);

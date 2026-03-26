namespace RelayChat.Node.Api;

public sealed record DeleteMessageRequest(
    Guid ChannelId,
    Guid MessageId,
    Guid AuthorId);

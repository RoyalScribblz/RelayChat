namespace RelayChat.Client;

public sealed record EditMessageRequest(
    Guid ChannelId,
    Guid MessageId,
    Guid AuthorId,
    string Content);

namespace RelayChat.Node.Contracts;

public sealed record EditMessageRequest(
    Guid ChannelId,
    Guid MessageId,
    Guid AuthorId,
    string Content);

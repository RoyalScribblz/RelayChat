namespace RelayChat.Node.Contracts;

public sealed record DeleteMessageRequest(
    Guid ChannelId,
    Guid MessageId);

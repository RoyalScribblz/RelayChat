namespace RelayChat.Node.Api;

public sealed record MessageDto(
    Guid Id,
    Guid ChannelId,
    Guid AuthorId,
    string Content,
    DateTimeOffset CreatedAt,
    Guid? ClientMessageId)
{
    public static MessageDto FromMessage(Message message)
    {
        return new MessageDto(
            message.Id,
            message.ChannelId,
            message.AuthorId,
            message.Content,
            message.CreatedAt,
            message.ClientMessageId);
    }
}

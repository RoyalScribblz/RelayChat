namespace RelayChat.Client;

public sealed record MessageDto(
    Guid Id,
    Guid ChannelId,
    Guid AuthorId,
    string Content,
    DateTimeOffset CreatedAt,
    Guid? ClientMessageId,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt);

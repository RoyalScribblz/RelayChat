namespace RelayChat.Node.Contracts;

public sealed record MessageDto(
    Guid Id,
    Guid ChannelId,
    Guid AuthorId,
    string AuthorName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Content,
    DateTimeOffset CreatedAt,
    Guid? ClientMessageId,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt);

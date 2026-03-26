namespace RelayChat.Node.Contracts;

public sealed record MembershipDto(
    Guid UserId,
    MembershipRole Role,
    string Name,
    string Handle,
    string? AvatarUrl);

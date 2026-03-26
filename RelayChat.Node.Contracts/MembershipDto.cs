namespace RelayChat.Node.Contracts;

public sealed record MembershipDto(Guid UserId, MembershipRole Role);

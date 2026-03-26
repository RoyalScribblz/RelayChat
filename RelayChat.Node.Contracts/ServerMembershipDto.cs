namespace RelayChat.Node.Contracts;

public sealed record ServerMembershipDto(Guid ServerId, Guid UserId, ServerMembershipRole Role);

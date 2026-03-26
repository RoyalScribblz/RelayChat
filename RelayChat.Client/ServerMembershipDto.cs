namespace RelayChat.Client;

public sealed record ServerMembershipDto(Guid ServerId, Guid UserId, ServerMembershipRole Role);

namespace RelayChat.Node.Contracts;

public sealed record CreateServerResultDto(ServerDto Server, ChannelDto Channel, ServerMembershipDto Membership);

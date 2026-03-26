using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public sealed record ServerMembershipDto(Guid ServerId, Guid UserId, ServerMembershipRole Role)
{
    public static ServerMembershipDto FromMembership(ServerMembership membership)
    {
        return new ServerMembershipDto(membership.ServerId, membership.UserId, membership.Role);
    }
}

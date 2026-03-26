namespace RelayChat.Node.Database;

public sealed class ServerMembership
{
    public Guid ServerId { get; set; }
    public Guid UserId { get; set; }
    public ServerMembershipRole Role { get; set; }
}

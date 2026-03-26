using RelayChat.Node.Contracts;

namespace RelayChat.Node.Database;

public sealed class Membership
{
    public Guid UserId { get; set; }
    public MembershipRole Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

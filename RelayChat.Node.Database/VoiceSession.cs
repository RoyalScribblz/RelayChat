namespace RelayChat.Node.Database;

public sealed class VoiceSession
{
    public Guid UserId { get; set; }
    public Guid ChannelId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

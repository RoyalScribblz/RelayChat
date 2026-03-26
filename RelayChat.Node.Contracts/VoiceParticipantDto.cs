namespace RelayChat.Node.Contracts;

public sealed record VoiceParticipantDto(
    Guid UserId,
    Guid ChannelId,
    string Name,
    string Handle,
    string? AvatarUrl,
    bool IsMuted);

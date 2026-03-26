namespace RelayChat.Node.Contracts;

public sealed record VoiceChannelStateDto(Guid ChannelId, IReadOnlyList<VoiceParticipantDto> Participants);

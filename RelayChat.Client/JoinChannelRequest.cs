namespace RelayChat.Client;

public sealed record JoinChannelRequest(Guid ChannelId, Guid AuthorId);

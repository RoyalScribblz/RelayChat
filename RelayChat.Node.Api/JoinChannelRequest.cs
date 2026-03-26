namespace RelayChat.Node.Api;

public sealed record JoinChannelRequest(Guid ChannelId, Guid AuthorId);

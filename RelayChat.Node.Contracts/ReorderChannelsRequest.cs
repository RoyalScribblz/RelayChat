namespace RelayChat.Node.Contracts;

public sealed record ReorderChannelsRequest(IReadOnlyList<Guid> ChannelIds);

namespace RelayChat.Node.Contracts;

public sealed record CreateChannelRequest(string Name, ChannelType Type);

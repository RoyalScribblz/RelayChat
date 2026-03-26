namespace RelayChat.Node.Contracts;

public sealed record CreateServerRequest(string Name, Guid CreatorId);

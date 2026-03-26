namespace RelayChat.Node.Api;

public sealed record NodeRelayTokensOptions(string Issuer, string NodeAudience, string SigningKey);

namespace RelayChat.ControlPlane.Api;

public sealed record RelayTokensOptions(
    string Issuer,
    string AccessAudience,
    string RefreshAudience,
    string NodeAudience,
    string SigningKey,
    int AccessTokenLifetimeMinutes,
    int RefreshTokenLifetimeDays,
    int NodeTokenLifetimeMinutes);

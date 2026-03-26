namespace RelayChat.ControlPlane.Api;

public sealed record UserProfileDto(
    Guid UserId,
    string Name,
    string Handle,
    string? AvatarUrl,
    string? Email);

public sealed record RelayTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Name,
    string Handle,
    string? AvatarUrl,
    string? Email);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record IssueNodeTokenRequest();

public sealed record NodeTokenResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId);

public sealed record UpdateProfileRequest(
    string Name,
    string Handle,
    string? AvatarUrl);

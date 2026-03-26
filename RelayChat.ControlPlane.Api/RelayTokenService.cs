using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RelayChat.ControlPlane.Database;

namespace RelayChat.ControlPlane.Api;

public sealed class RelayTokenService(RelayTokensOptions options)
{
    private readonly JwtSecurityTokenHandler tokenHandler = new();
    private readonly SigningCredentials signingCredentials =
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);

    public RelayTokensResponse IssueTokens(User user)
    {
        var accessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenLifetimeMinutes);
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(options.RefreshTokenLifetimeDays);

        var accessToken = CreateToken(
            user,
            options.AccessAudience,
            RelayTokenTypes.Access,
            accessExpiresAt);
        var refreshToken = CreateToken(
            user,
            options.RefreshAudience,
            RelayTokenTypes.Refresh,
            refreshExpiresAt);

        return new RelayTokensResponse(
            accessToken,
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            user.Id,
            user.Name,
            user.Handle,
            user.AvatarUrl,
            user.Email);
    }

    public NodeTokenResponse IssueNodeToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.NodeTokenLifetimeMinutes);
        var token = CreateToken(
            user,
            options.NodeAudience,
            RelayTokenTypes.Node,
            expiresAt,
            [new Claim(RelayClaimTypes.Scope, "node:access")]);

        return new NodeTokenResponse(token, expiresAt, user.Id);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var principal = tokenHandler.ValidateToken(
                refreshToken,
                CreateTokenValidationParameters(options, options.RefreshAudience),
                out _);

            var tokenType = principal.FindFirst(RelayClaimTypes.TokenType)?.Value;
            return tokenType == RelayTokenTypes.Refresh ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    public static TokenValidationParameters CreateTokenValidationParameters(
        RelayTokensOptions options,
        string audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
    }

    private string CreateToken(
        User user,
        string audience,
        string tokenType,
        DateTimeOffset expiresAt,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(RelayClaimTypes.TokenType, tokenType),
            new("name", user.Name),
            new("preferred_username", user.Handle)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim("email", user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim("picture", user.AvatarUrl));
        }

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return tokenHandler.WriteToken(token);
    }
}

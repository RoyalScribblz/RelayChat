using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;
using System.Text.Json;

namespace RelayChat.Node.Api;

public sealed class LiveKitTokenService(LiveKitOptions options)
{
    private readonly JwtSecurityTokenHandler tokenHandler = new();
    private readonly SigningCredentials signingCredentials =
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.ApiSecret)), SecurityAlgorithms.HmacSha256);

    public VoiceChannelAccessDto IssueVoiceAccessToken(Channel channel, ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var metadata = new Dictionary<string, string?>
        {
            ["handle"] = user.GetHandle()
        };

        var avatarUrl = user.GetAvatarUrl();
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            metadata["avatarUrl"] = avatarUrl;
        }

        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Sub, userId.ToString() },
            { JwtRegisteredClaimNames.Nbf, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Exp, expiresAt.ToUnixTimeSeconds() },
            { "name", user.GetDisplayName() },
            { "metadata", JsonSerializer.Serialize(metadata) },
            {
                "video",
                new Dictionary<string, object>
                {
                    ["room"] = channel.Id.ToString(),
                    ["roomJoin"] = true,
                    ["canPublish"] = true,
                    ["canSubscribe"] = true
                }
            }
        };

        var header = new JwtHeader(signingCredentials);

        payload[JwtRegisteredClaimNames.Iss] = options.ApiKey;

        var token = new JwtSecurityToken(header, payload);

        return new VoiceChannelAccessDto(options.WebSocketUrl, tokenHandler.WriteToken(token));
    }
}

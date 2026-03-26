using System.Security.Claims;

namespace RelayChat.Node.Api;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("Missing subject claim.");

        return Guid.Parse(subject);
    }

    public static bool HasNodeAccess(this ClaimsPrincipal principal)
    {
        return principal.FindAll(NodeRelayClaimTypes.Scope)
            .Any(claim => claim.Value == "node:access");
    }
}

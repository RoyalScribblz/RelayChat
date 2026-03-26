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

    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("name")?.Value
               ?? principal.FindFirst("preferred_username")?.Value
               ?? principal.Identity?.Name
               ?? principal.GetRequiredUserId().ToString();
    }

    public static string GetHandle(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("preferred_username")?.Value
               ?? principal.GetDisplayName();
    }

    public static string? GetAvatarUrl(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("picture")?.Value;
    }
}

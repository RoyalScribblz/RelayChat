using System.Security.Claims;

namespace RelayChat.ControlPlane.Api;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Missing subject claim.");

        return Guid.Parse(subject);
    }

    public static string GetRequiredExternalSubject(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("sub")?.Value
               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? throw new InvalidOperationException("Missing external subject claim.");
    }

    public static string? GetExternalDisplayName(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("name")?.Value
               ?? principal.FindFirst("preferred_username")?.Value
               ?? principal.Identity?.Name;
    }

    public static string? GetExternalHandleCandidate(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("preferred_username")?.Value
               ?? principal.FindFirst("nickname")?.Value
               ?? principal.FindFirst("email")?.Value?.Split('@')[0]
               ?? principal.GetExternalDisplayName();
    }

    public static string? GetExternalEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("email")?.Value;
    }

    public static string? GetExternalAvatarUrl(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("picture")?.Value;
    }
}

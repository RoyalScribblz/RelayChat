using System.Security.Claims;
using System.Text;
using RelayChat.ControlPlane.Database;

namespace RelayChat.ControlPlane.Api;

public sealed class UserProvisioningService(UserRepository userRepository)
{
    private const string Provider = "authentik";

    public async Task<User> GetOrCreateUser(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var subject = principal.GetRequiredExternalSubject();
        var existing = await userRepository.GetByExternalLogin(Provider, subject, ct);
        if (existing is not null)
        {
            var email = principal.GetExternalEmail();
            if (!string.Equals(existing.Email, email, StringComparison.Ordinal))
            {
                existing.Email = email;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await userRepository.SaveChanges(ct);
            }

            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var handle = await GenerateHandle(principal.GetExternalHandleCandidate(), null, ct);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = NormalizeName(principal.GetExternalDisplayName()),
            Handle = handle,
            HandleNormalized = NormalizeHandle(handle),
            AvatarUrl = NormalizeAvatarUrl(principal.GetExternalAvatarUrl()),
            Email = principal.GetExternalEmail(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var externalLogin = new ExternalLogin
        {
            Provider = Provider,
            Subject = subject,
            UserId = user.Id
        };

        await userRepository.Add(user, externalLogin, ct);
        return user;
    }

    public async Task<string> GenerateHandle(string? candidate, Guid? excludingUserId, CancellationToken ct = default)
    {
        var normalizedBase = NormalizeHandle(candidate);
        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var normalized = suffix == 0 ? normalizedBase : AppendSuffix(normalizedBase, suffix);
            if (!await userRepository.HandleExists(normalized, excludingUserId, ct))
            {
                return normalized;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique handle.");
    }

    public static string NormalizeName(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Relay User" : trimmed[..Math.Min(trimmed.Length, 64)];
    }

    public static string NormalizeHandle(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "user" : value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (character is '_' or '-' or ' ' or '.')
            {
                builder.Append('_');
            }
        }

        var normalized = builder.ToString().Trim('_');
        if (normalized.Length < 3)
        {
            normalized = $"{normalized}user";
        }

        if (normalized.Length > 32)
        {
            normalized = normalized[..32];
        }

        return normalized;
    }

    public static string? NormalizeAvatarUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.ToString() : null;
    }

    private static string AppendSuffix(string normalizedBase, int suffix)
    {
        var suffixText = suffix.ToString();
        var maxBaseLength = 32 - suffixText.Length - 1;
        var baseText = normalizedBase.Length > maxBaseLength ? normalizedBase[..maxBaseLength] : normalizedBase;
        return $"{baseText}_{suffixText}";
    }
}

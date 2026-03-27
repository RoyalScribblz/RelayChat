using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RelayChat.Client.Services;

public sealed class ControlPlaneApiClient(AuthService authService, ControlPlaneApiOptions options)
{
    public async Task<UserProfileDto?> GetCurrentUser(CancellationToken ct = default)
    {
        using var client = await CreateAuthorizedClient(ct);
        return await client.GetFromJsonAsync<UserProfileDto>("/users/me", ct);
    }

    public async Task<UserProfileDto?> UpdateCurrentUser(UpdateProfileRequest request, CancellationToken ct = default)
    {
        using var client = await CreateAuthorizedClient(ct);
        using var response = await client.PutAsJsonAsync("/users/me", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            var error = TryExtractError(payload);
            throw new InvalidOperationException(error ?? $"Profile update failed with status code {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<UserProfileDto>(ct);
    }

    private async Task<HttpClient> CreateAuthorizedClient(CancellationToken ct)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl)
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await authService.GetAccessToken());
        return client;
    }

    private static string? TryExtractError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("error", out var errorProperty)
                ? errorProperty.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}

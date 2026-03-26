using System.Net.Http.Headers;
using System.Net.Http.Json;

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
        response.EnsureSuccessStatusCode();
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
}

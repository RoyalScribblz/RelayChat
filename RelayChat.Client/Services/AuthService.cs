using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace RelayChat.Client.Services;

public sealed class AuthService(
    HttpClient httpClient,
    IJSRuntime jsRuntime,
    NavigationManager navigationManager,
    ControlPlaneApiOptions controlPlaneApiOptions)
{
    private const string StorageKey = "relaychat.auth.tokens";
    private readonly Dictionary<string, NodeTokenResponse> nodeTokens = [];
    private RelayTokensResponse? tokens;
    private bool initialized;

    public bool IsAuthenticated => tokens is not null;
    public Guid? UserId => tokens?.UserId;
    public string? Name => tokens?.Name;
    public string? Handle => tokens?.Handle;
    public string? AvatarUrl => tokens?.AvatarUrl;
    public string? Email => tokens?.Email;

    public async Task Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var state = JsonSerializer.Deserialize<PersistedAuthState>(json);
        tokens = state?.Tokens;
    }

    public string GetLoginUrl(string? returnUrl = null)
    {
        var relativeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        var callbackUri = new Uri(new Uri(navigationManager.BaseUri), $"auth/complete?returnUrl={Uri.EscapeDataString(relativeReturnUrl)}");
        return $"{controlPlaneApiOptions.BaseUrl.TrimEnd('/')}/auth/login?returnUrl={Uri.EscapeDataString(callbackUri.ToString())}";
    }

    public async Task Exchange()
    {
        await Initialize();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{controlPlaneApiOptions.BaseUrl.TrimEnd('/')}/auth/exchange");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        tokens = await response.Content.ReadFromJsonAsync<RelayTokensResponse>()
            ?? throw new InvalidOperationException("The control plane did not return a token response.");
        nodeTokens.Clear();
        await Persist();
    }

    public async Task Clear()
    {
        tokens = null;
        nodeTokens.Clear();
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }

    public async Task<string> GetAccessToken()
    {
        await Initialize();
        if (tokens is null)
        {
            throw new InvalidOperationException("The user is not authenticated.");
        }

        if (IsExpiring(tokens.AccessTokenExpiresAt))
        {
            await Refresh();
        }

        return tokens.AccessToken;
    }

    public async Task<string> GetNodeToken()
    {
        await Initialize();
        const string cacheKey = "node";

        if (nodeTokens.TryGetValue(cacheKey, out var cached) && !IsExpiring(cached.ExpiresAt))
        {
            return cached.Token;
        }

        var accessToken = await GetAccessToken();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{controlPlaneApiOptions.BaseUrl.TrimEnd('/')}/auth/node-token")
        {
            Content = JsonContent.Create(new IssueNodeTokenRequest())
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<NodeTokenResponse>()
            ?? throw new InvalidOperationException("The control plane did not return a node token.");
        nodeTokens[cacheKey] = token;
        return token.Token;
    }

    public async Task ApplyProfile(UserProfileDto profile)
    {
        await Initialize();
        if (tokens is null)
        {
            return;
        }

        tokens = tokens with
        {
            Name = profile.Name,
            Handle = profile.Handle,
            AvatarUrl = profile.AvatarUrl,
            Email = profile.Email
        };
        await Persist();
    }

    private async Task Refresh()
    {
        if (tokens is null)
        {
            throw new InvalidOperationException("The user is not authenticated.");
        }

        using var response = await httpClient.PostAsJsonAsync(
            $"{controlPlaneApiOptions.BaseUrl.TrimEnd('/')}/auth/refresh",
            new RefreshTokenRequest(tokens.RefreshToken));
        response.EnsureSuccessStatusCode();

        tokens = await response.Content.ReadFromJsonAsync<RelayTokensResponse>()
            ?? throw new InvalidOperationException("The control plane did not return refreshed tokens.");
        nodeTokens.Clear();
        await Persist();
    }

    private async Task Persist()
    {
        var json = JsonSerializer.Serialize(new PersistedAuthState(tokens));
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    private static bool IsExpiring(DateTimeOffset expiresAt)
    {
        return expiresAt <= DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private sealed record PersistedAuthState(RelayTokensResponse? Tokens);
}

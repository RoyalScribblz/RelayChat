using Microsoft.JSInterop;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Services;

public sealed class VoiceClient(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? module;
    private DotNetObjectReference<VoiceClient>? callbackReference;

    public Guid? ActiveChannelId { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsConnected => ActiveChannelId.HasValue;
    public event Action<IReadOnlySet<Guid>>? ActiveSpeakersChanged;

    public async Task Join(Guid channelId, VoiceChannelAccessDto access)
    {
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/livekitInterop.js");
        callbackReference ??= DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("joinVoiceChannel", access.ServerUrl, access.Token, callbackReference);
        ActiveChannelId = channelId;
        IsMuted = false;
    }

    public async Task Leave()
    {
        if (module is null)
        {
            ActiveChannelId = null;
            IsMuted = false;
            return;
        }

        await module.InvokeVoidAsync("leaveVoiceChannel");
        ActiveChannelId = null;
        IsMuted = false;
        ActiveSpeakersChanged?.Invoke(new HashSet<Guid>());
    }

    public async Task SetMuted(bool isMuted)
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("setVoiceMuted", isMuted);
        IsMuted = isMuted;
    }

    [JSInvokable]
    public Task HandleActiveSpeakersChanged(string[] identities)
    {
        var activeSpeakers = identities
            .Select(identity => Guid.TryParse(identity, out var userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .ToHashSet();
        ActiveSpeakersChanged?.Invoke(activeSpeakers);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("leaveVoiceChannel");
                await module.DisposeAsync();
            }
            catch
            {
                // Ignore teardown errors during disposal.
            }
        }

        callbackReference?.Dispose();
    }
}

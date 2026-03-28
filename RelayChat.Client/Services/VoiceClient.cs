using Microsoft.JSInterop;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Services;

public sealed record ScreenShareResult(bool Started, bool AudioIncluded, bool FellBackToVideoOnly);

public sealed class VoiceClient(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? module;
    private DotNetObjectReference<VoiceClient>? callbackReference;

    public Guid? ActiveChannelId { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsDeafened { get; private set; }
    public bool IsCameraEnabled { get; private set; }
    public bool IsScreenShareEnabled { get; private set; }
    public bool IsConnected => ActiveChannelId.HasValue;
    public event Action<IReadOnlySet<Guid>>? ActiveSpeakersChanged;
    public event Action<IReadOnlySet<Guid>>? VideoParticipantsChanged;

    public async Task Join(Guid channelId, VoiceChannelAccessDto access)
    {
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/livekitInterop.js");
        callbackReference ??= DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("joinVoiceChannel", access.ServerUrl, access.Token, callbackReference);
        ActiveChannelId = channelId;
        IsMuted = false;
        IsDeafened = false;
        IsCameraEnabled = false;
        IsScreenShareEnabled = false;
    }

    public async Task Leave()
    {
        if (module is null)
        {
            ActiveChannelId = null;
            IsMuted = false;
            IsDeafened = false;
            return;
        }

        await module.InvokeVoidAsync("leaveVoiceChannel");
        ActiveChannelId = null;
        IsMuted = false;
        IsDeafened = false;
        IsCameraEnabled = false;
        IsScreenShareEnabled = false;
        ActiveSpeakersChanged?.Invoke(new HashSet<Guid>());
        VideoParticipantsChanged?.Invoke(new HashSet<Guid>());
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

    public async Task SetDeafened(bool isDeafened)
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("setVoiceDeafened", isDeafened);
        IsDeafened = isDeafened;
    }

    public async Task SetCameraEnabled(bool isEnabled)
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("setCameraEnabled", isEnabled);
        IsCameraEnabled = isEnabled;
    }

    public async Task SetVoiceParticipants(IReadOnlyList<VoiceParticipantDto> participants)
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("setVoiceParticipants", participants);
    }

    public async Task<ScreenShareResult> SetScreenShareEnabled(bool isEnabled)
    {
        if (module is null)
        {
            return new ScreenShareResult(false, false, false);
        }

        var result = await module.InvokeAsync<ScreenShareResult>("setScreenShareEnabled", isEnabled);
        IsScreenShareEnabled = result.Started;
        return result;
    }

    public async Task<int> GetParticipantVolume(Guid userId, string source)
    {
        if (module is null)
        {
            return 100;
        }

        return await module.InvokeAsync<int>("getParticipantVolume", userId.ToString(), source);
    }

    public async Task SetParticipantVolume(Guid userId, string source, int volumePercent)
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("setParticipantVolume", userId.ToString(), source, volumePercent);
    }

    public async Task RefreshGrid()
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("refreshVoiceGrid");
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

    [JSInvokable]
    public Task HandleVideoParticipantsChanged(string[] identities)
    {
        var videoParticipants = identities
            .Select(identity => Guid.TryParse(identity, out var userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .ToHashSet();
        VideoParticipantsChanged?.Invoke(videoParticipants);
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

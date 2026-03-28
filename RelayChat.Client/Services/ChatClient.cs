using Microsoft.AspNetCore.SignalR.Client;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Services;

public sealed class ChatClient : IAsyncDisposable
{
    private readonly AuthService authService;
    private readonly HubConnection connection;
    private Guid? joinedChannelId;
    private Guid? joinedVoiceChannelId;

    public ChatClient(NodeApiOptions options, AuthService authService)
    {
        this.authService = authService;
        connection = new HubConnectionBuilder()
            .WithUrl($"{options.BaseUrl.TrimEnd('/')}/chathub", hubOptions =>
            {
                hubOptions.AccessTokenProvider = async () =>
                {
                    if (!joinedChannelId.HasValue && !joinedVoiceChannelId.HasValue)
                    {
                        return null;
                    }

                    return await authService.GetNodeToken();
                };
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<MessageDto>("ReceiveMessage", message => MessageReceived?.Invoke(message));
        connection.On<MessageDto>("ReceiveMessageUpdated", message => MessageUpdated?.Invoke(message));
        connection.On<MembershipDto>("ReceiveMemberUpdated", membership => MemberUpdated?.Invoke(membership));
        connection.On<VoiceChannelStateDto>("ReceiveVoiceChannelState", state => VoiceChannelStateReceived?.Invoke(state));
        connection.Reconnected += async _ =>
        {
            if (joinedChannelId.HasValue)
            {
                await JoinChannel(joinedChannelId.Value);
            }

            if (joinedVoiceChannelId.HasValue)
            {
                await JoinVoiceChannel(joinedVoiceChannelId.Value);
            }

            if (Reconnected is not null)
            {
                await Reconnected.Invoke();
            }
        };
    }

    public event Action<MessageDto>? MessageReceived;
    public event Action<MessageDto>? MessageUpdated;
    public event Action<MembershipDto>? MemberUpdated;
    public event Action<VoiceChannelStateDto>? VoiceChannelStateReceived;
    public event Func<Task>? Reconnected;

    public async Task Connect(CancellationToken ct = default)
    {
        if (connection.State == HubConnectionState.Disconnected)
        {
            await connection.StartAsync(ct);
        }
    }

    public async Task JoinChannel(Guid channelId, CancellationToken ct = default)
    {
        joinedChannelId = channelId;
        await Connect(ct);
        await connection.InvokeAsync("JoinChannel", new JoinChannelRequest(channelId), ct);
    }

    public async Task JoinVoiceChannel(Guid channelId, CancellationToken ct = default)
    {
        joinedVoiceChannelId = channelId;
        await Connect(ct);
        await connection.InvokeAsync("JoinVoiceChannel", channelId, ct);
    }

    public async Task LeaveVoiceChannel(CancellationToken ct = default)
    {
        joinedVoiceChannelId = null;
        await Connect(ct);
        await connection.InvokeAsync("LeaveVoiceChannel", ct);
    }

    public async Task SetVoiceMuted(bool isMuted, CancellationToken ct = default)
    {
        await Connect(ct);
        await connection.InvokeAsync("SetVoiceMuted", isMuted, ct);
    }

    public async Task SetVoiceDeafened(bool isDeafened, CancellationToken ct = default)
    {
        await Connect(ct);
        await connection.InvokeAsync("SetVoiceDeafened", isDeafened, ct);
    }

    public Task SendMessage(SendMessageRequest request, CancellationToken ct = default)
    {
        return connection.InvokeAsync("SendMessage", request, ct);
    }

    public Task EditMessage(EditMessageRequest request, CancellationToken ct = default)
    {
        return connection.InvokeAsync("EditMessage", request, ct);
    }

    public Task DeleteMessage(DeleteMessageRequest request, CancellationToken ct = default)
    {
        return connection.InvokeAsync("DeleteMessage", request, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

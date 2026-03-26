using Microsoft.AspNetCore.SignalR.Client;

namespace RelayChat.Client.Services;

public sealed class ChatClient : IAsyncDisposable
{
    private readonly HubConnection connection;
    private Guid? joinedChannelId;

    public ChatClient(NodeApiOptions options)
    {
        connection = new HubConnectionBuilder()
            .WithUrl($"{options.BaseUrl.TrimEnd('/')}/chathub")
            .WithAutomaticReconnect()
            .Build();

        connection.On<MessageDto>("ReceiveMessage", message => MessageReceived?.Invoke(message));
        connection.On<MessageDto>("ReceiveMessageUpdated", message => MessageUpdated?.Invoke(message));
        connection.Reconnected += async _ =>
        {
            if (joinedChannelId.HasValue)
            {
                await JoinChannel(joinedChannelId.Value);
            }

            if (Reconnected is not null)
            {
                await Reconnected.Invoke();
            }
        };
    }

    public Guid UserId { get; } = Guid.NewGuid();

    public event Action<MessageDto>? MessageReceived;
    public event Action<MessageDto>? MessageUpdated;
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
        await connection.InvokeAsync("JoinChannel", new JoinChannelRequest(channelId, UserId), ct);
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

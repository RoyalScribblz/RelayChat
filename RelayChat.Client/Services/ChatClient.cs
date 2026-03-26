using Microsoft.AspNetCore.SignalR.Client;

namespace RelayChat.Client.Services;

public sealed class ChatClient : IAsyncDisposable
{
    private readonly HubConnection connection;
    private Guid? joinedChannelId;

    public ChatClient()
    {
        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5002/chathub")
            .WithAutomaticReconnect()
            .Build();

        connection.On<MessageDto>("ReceiveMessage", message => MessageReceived?.Invoke(message));
        connection.Reconnected += async _ =>
        {
            if (joinedChannelId.HasValue)
            {
                await JoinChannel(joinedChannelId.Value);
            }
        };
    }

    public Guid UserId { get; } = Guid.NewGuid();

    public event Action<MessageDto>? MessageReceived;

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

    public Task SendMessage(SendMessageRequest request, CancellationToken ct = default)
    {
        return connection.InvokeAsync("SendMessage", request, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Net.Http.Json;
using RelayChat.Client.Services;
using MudBlazor;

namespace RelayChat.Client.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    private const int HistoryPageSize = 100;
    private readonly List<ChatMessage> messages = [];
    private readonly List<ChannelDto> channels = [];
    private readonly List<ServerDto> servers = [];
    private Guid? joinedChannelId;
    private string messageText = string.Empty;
    private HttpClient? httpClient;

    [Parameter]
    public Guid ServerId { get; set; }

    [Parameter]
    public Guid ChannelId { get; set; }

    [Inject]
    public required ChatClient ChatClient { get; init; }

    [Inject]
    public required NodeApiOptions NodeApiOptions { get; init; }

    protected Guid _channelId => ChannelId;
    protected IReadOnlyList<ServerDto> _servers => servers;
    protected string _serverName { get; private set; } = string.Empty;
    protected string _channelName { get; private set; } = string.Empty;
    protected IReadOnlyList<ChannelDto> _channels => channels;
    protected IReadOnlyList<ChatMessage> _messages => messages;
    protected string _messageText
    {
        get => messageText;
        set => messageText = value;
    }

    protected override async Task OnInitializedAsync()
    {
        httpClient = new HttpClient { BaseAddress = new Uri(NodeApiOptions.BaseUrl) };
        ChatClient.MessageReceived += OnMessageReceived;
        ChatClient.Reconnected += OnReconnected;
    }

    protected override async Task OnParametersSetAsync()
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        await LoadChannelContext();
        if (joinedChannelId != ChannelId)
        {
            messages.Clear();
            messageText = string.Empty;
        }

        var history = await GetMessages(limit: HistoryPageSize);
        messages.Clear();
        foreach (var message in history)
        {
            MergeConfirmedMessage(message);
        }

        await ChatClient.JoinChannel(ChannelId);
        joinedChannelId = ChannelId;
    }

    protected async Task SendMessage()
    {
        var content = messageText.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var pendingMessage = new ChatMessage
        {
            Message = new MessageDto(
                Guid.NewGuid(),
                ChannelId,
                ChatClient.UserId,
                content,
                DateTimeOffset.UtcNow,
                Guid.NewGuid()),
            State = MessageState.Pending,
            IsOwnMessage = true
        };

        messageText = string.Empty;
        messages.Add(pendingMessage);
        SortMessages();
        await InvokeAsync(StateHasChanged);

        try
        {
            await ChatClient.SendMessage(new SendMessageRequest(
                pendingMessage.Message.ChannelId,
                pendingMessage.Message.AuthorId,
                pendingMessage.Message.Content,
                pendingMessage.Message.ClientMessageId!.Value));
        }
        catch
        {
            pendingMessage.State = MessageState.Failed;
            SortMessages();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await SendMessage();
        }
    }

    public void Dispose()
    {
        ChatClient.MessageReceived -= OnMessageReceived;
        ChatClient.Reconnected -= OnReconnected;
        httpClient?.Dispose();
    }

    private void OnMessageReceived(MessageDto message)
    {
        if (message.ChannelId != ChannelId)
        {
            return;
        }

        MergeConfirmedMessage(message);
        _ = InvokeAsync(StateHasChanged);
    }

    protected Color GetStateColor(MessageState state)
    {
        return state switch
        {
            MessageState.Pending => Color.Warning,
            MessageState.Confirmed => Color.Success,
            MessageState.Failed => Color.Error,
            _ => Color.Default
        };
    }

    private ChatMessage ToChatMessage(MessageDto message, MessageState state)
    {
        return new ChatMessage
        {
            Message = message,
            State = state,
            IsOwnMessage = message.AuthorId == ChatClient.UserId
        };
    }

    private void SortMessages()
    {
        messages.Sort((left, right) =>
        {
            var createdAtComparison = left.Message.CreatedAt.CompareTo(right.Message.CreatedAt);
            return createdAtComparison != 0
                ? createdAtComparison
                : left.Message.Id.CompareTo(right.Message.Id);
        });
    }

    private async Task OnReconnected()
    {
        await RecoverMissingMessages();
        await InvokeAsync(StateHasChanged);
    }

    private void MergeConfirmedMessage(MessageDto message)
    {
        var existing = messages.FirstOrDefault(current => current.Message.Id == message.Id);
        if (existing is not null)
        {
            if (existing.State != MessageState.Confirmed)
            {
                existing.Message = message;
                existing.State = MessageState.Confirmed;
                SortMessages();
            }

            return;
        }

        var pendingMessage = messages.FirstOrDefault(current =>
            current.State == MessageState.Pending &&
            current.IsOwnMessage &&
            current.Message.ClientMessageId.HasValue &&
            current.Message.ClientMessageId == message.ClientMessageId);

        if (pendingMessage is not null)
        {
            pendingMessage.Message = message;
            pendingMessage.State = MessageState.Confirmed;
            SortMessages();
            return;
        }

        if (message.ClientMessageId.HasValue &&
            messages.Any(current =>
                current.Message.ClientMessageId.HasValue &&
                current.Message.ClientMessageId == message.ClientMessageId))
        {
            return;
        }

        messages.Add(ToChatMessage(message, MessageState.Confirmed));
        SortMessages();
    }

    private async Task RecoverMissingMessages()
    {
        while (true)
        {
            var missingMessages = await GetMessages(after: GetLastConfirmedMessageId(), limit: HistoryPageSize);
            if (missingMessages.Count == 0)
            {
                return;
            }

            foreach (var message in missingMessages)
            {
                MergeConfirmedMessage(message);
            }

            if (missingMessages.Count < HistoryPageSize)
            {
                return;
            }
        }
    }

    private async Task<List<MessageDto>> GetMessages(Guid? before = null, Guid? after = null, int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var query = new List<string>();
        if (before.HasValue)
        {
            query.Add($"before={before.Value}");
        }

        if (after.HasValue)
        {
            query.Add($"after={after.Value}");
        }

        if (limit.HasValue)
        {
            query.Add($"limit={limit.Value}");
        }

        var path = $"/servers/{ServerId}/channels/{ChannelId}/messages";
        if (query.Count > 0)
        {
            path = $"{path}?{string.Join("&", query)}";
        }

        return await httpClient.GetFromJsonAsync<List<MessageDto>>(path) ?? [];
    }

    private Guid? GetLastConfirmedMessageId()
    {
        return messages
            .Where(message => message.State == MessageState.Confirmed)
            .OrderByDescending(message => message.Message.CreatedAt)
            .ThenByDescending(message => message.Message.Id)
            .Select(message => (Guid?)message.Message.Id)
            .FirstOrDefault();
    }

    private async Task LoadChannelContext()
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var servers = await httpClient.GetFromJsonAsync<List<ServerDto>>("/servers") ?? [];
        this.servers.Clear();
        this.servers.AddRange(servers);

        var server = servers.FirstOrDefault(current => current.Id == ServerId)
            ?? throw new InvalidOperationException($"Server '{ServerId}' was not returned by the node API.");

        var availableChannels = await httpClient.GetFromJsonAsync<List<ChannelDto>>($"/servers/{ServerId}/channels") ?? [];
        var channel = availableChannels.FirstOrDefault(current => current.Id == ChannelId)
            ?? throw new InvalidOperationException($"Channel '{ChannelId}' was not returned by the node API.");

        _serverName = server.Name;
        _channelName = channel.Name;
        channels.Clear();
        channels.AddRange(availableChannels);
    }

    protected string GetChannelHref(ChannelDto channel)
    {
        return $"/servers/{ServerId}/channels/{channel.Id}";
    }

    protected string GetServerHref(ServerDto server)
    {
        if (server.Id == ServerId && channels.Count > 0)
        {
            return GetChannelHref(channels[0]);
        }

        return $"/servers/{server.Id}";
    }
}

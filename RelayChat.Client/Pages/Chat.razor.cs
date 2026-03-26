using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Net.Http.Json;
using RelayChat.Client.Services;
using MudBlazor;

namespace RelayChat.Client.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    private static readonly Guid ChannelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const int HistoryPageSize = 100;
    private readonly HttpClient httpClient = new() { BaseAddress = new Uri("http://localhost:5002") };
    private readonly List<ChatMessage> messages = [];
    private string messageText = string.Empty;

    [Inject]
    public required ChatClient ChatClient { get; init; }

    protected Guid _channelId => ChannelId;
    protected IReadOnlyList<ChatMessage> _messages => messages;
    protected string _messageText
    {
        get => messageText;
        set => messageText = value;
    }

    protected override async Task OnInitializedAsync()
    {
        ChatClient.MessageReceived += OnMessageReceived;
        ChatClient.Reconnected += OnReconnected;

        var history = await GetMessages(limit: HistoryPageSize);
        messages.Clear();
        foreach (var message in history)
        {
            MergeConfirmedMessage(message);
        }

        await ChatClient.JoinChannel(ChannelId);
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
        httpClient.Dispose();
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

        var path = $"/channels/{ChannelId}/messages";
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
}

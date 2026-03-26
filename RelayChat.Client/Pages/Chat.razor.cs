using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Net.Http.Json;
using RelayChat.Client.Services;
using MudBlazor;

namespace RelayChat.Client.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    private static readonly Guid ChannelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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

        var history = await httpClient.GetFromJsonAsync<List<MessageDto>>($"/channels/{ChannelId}/messages") ?? [];
        messages.Clear();
        messages.AddRange(history.Select(message => ToChatMessage(message, MessageState.Confirmed)));
        SortMessages();

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
        httpClient.Dispose();
    }

    private void OnMessageReceived(MessageDto message)
    {
        if (message.ChannelId != ChannelId)
        {
            return;
        }

        if (messages.Any(existing => existing.Message.Id == message.Id))
        {
            return;
        }

        var pendingMessage = messages.FirstOrDefault(existing =>
            existing.State == MessageState.Pending &&
            existing.IsOwnMessage &&
            existing.Message.ClientMessageId.HasValue &&
            existing.Message.ClientMessageId == message.ClientMessageId);

        if (pendingMessage is not null)
        {
            pendingMessage.Message = message;
            pendingMessage.State = MessageState.Confirmed;
            SortMessages();
            _ = InvokeAsync(StateHasChanged);
            return;
        }

        if (messages.Any(existing =>
                existing.Message.ClientMessageId.HasValue &&
                existing.Message.ClientMessageId == message.ClientMessageId))
        {
            return;
        }

        messages.Add(ToChatMessage(message, MessageState.Confirmed));
        SortMessages();
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
        messages.Sort((left, right) => left.Message.CreatedAt.CompareTo(right.Message.CreatedAt));
    }
}

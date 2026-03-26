using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using RelayChat.Client.Services;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Pages;

public partial class Chat : ComponentBase, IAsyncDisposable
{
    private const int HistoryPageSize = 100;
    private readonly List<ChatMessage> messages = [];
    private readonly List<ChannelDto> channels = [];
    private readonly List<VoiceParticipantDto> voiceParticipants = [];
    private HashSet<Guid> activeSpeakerIds = [];
    private Guid? joinedChannelId;
    private string messageText = string.Empty;
    private string editText = string.Empty;
    private string newChannelName = string.Empty;
    private Guid? editingMessageId;
    private ChannelType newChannelType = ChannelType.Text;
    private ChannelType channelType = ChannelType.Text;

    [Parameter]
    public Guid ChannelId { get; set; }

    [Inject]
    public required AuthService AuthService { get; init; }

    [Inject]
    public required ChatClient ChatClient { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required NodeApiClient NodeApiClient { get; init; }

    [Inject]
    public required VoiceClient VoiceClient { get; init; }

    protected Guid _channelId => ChannelId;
    protected bool _isAuthenticated { get; private set; }
    protected MembershipRole? _membershipRole { get; private set; }
    protected string _nodeName { get; private set; } = "Relay";
    protected string _channelName { get; private set; } = string.Empty;
    protected ChannelType _channelType => channelType;
    protected bool _isVoiceChannel => channelType == ChannelType.Voice;
    protected bool _isConnectedToVoiceChannel => VoiceClient.ActiveChannelId == ChannelId;
    protected bool _isVoiceMuted => VoiceClient.IsMuted;
    protected IReadOnlyList<ChannelDto> _channels => channels;
    protected IReadOnlyList<ChatMessage> _messages => messages;
    protected IReadOnlyList<VoiceParticipantDto> _voiceParticipants => voiceParticipants;
    protected Guid? _editingMessageId => editingMessageId;
    protected ChannelType _newChannelType
    {
        get => newChannelType;
        set => newChannelType = value;
    }
    protected string _editText
    {
        get => editText;
        set => editText = value;
    }
    protected string _newChannelName
    {
        get => newChannelName;
        set => newChannelName = value;
    }
    protected string _messageText
    {
        get => messageText;
        set => messageText = value;
    }

    protected override async Task OnInitializedAsync()
    {
        await AuthService.Initialize();
        ChatClient.MessageReceived += OnMessageReceived;
        ChatClient.MessageUpdated += OnMessageUpdated;
        ChatClient.VoiceChannelStateReceived += OnVoiceChannelStateReceived;
        ChatClient.Reconnected += OnReconnected;
        VoiceClient.ActiveSpeakersChanged += OnActiveSpeakersChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadChannelContext();

        if (!_isAuthenticated || _membershipRole is null)
        {
            await LeaveVoiceChannel();
            joinedChannelId = null;
            return;
        }

        if (_isVoiceChannel)
        {
            messages.Clear();
            editingMessageId = null;
            editText = string.Empty;
            await LoadVoiceChannel();
            joinedChannelId = null;
            return;
        }

        if (VoiceClient.ActiveChannelId.HasValue)
        {
            await LeaveVoiceChannel();
        }

        if (joinedChannelId != ChannelId)
        {
            messages.Clear();
            messageText = string.Empty;
            editingMessageId = null;
            editText = string.Empty;
        }

        messages.Clear();
        var history = await NodeApiClient.GetMessages(ChannelId, limit: HistoryPageSize);
        foreach (var message in history)
        {
            MergeConfirmedMessage(message);
        }

        await ChatClient.JoinChannel(ChannelId);
        joinedChannelId = ChannelId;
    }

    protected void Login()
    {
        NavigationManager.NavigateTo(
            AuthService.GetLoginUrl(NavigationManager.ToBaseRelativePath(NavigationManager.Uri)),
            forceLoad: true);
    }

    protected async Task JoinNode()
    {
        if (!_isAuthenticated)
        {
            Login();
            return;
        }

        var membership = await NodeApiClient.JoinNode();
        _membershipRole = membership?.Role;
    }

    protected async Task SendMessage()
    {
        if (_isVoiceChannel || !_isAuthenticated || _membershipRole is null || !AuthService.UserId.HasValue)
        {
            return;
        }

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
                AuthService.UserId.Value,
                content,
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                null,
                null),
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

    protected async Task ToggleMute()
    {
        var newMutedState = !VoiceClient.IsMuted;
        await VoiceClient.SetMuted(newMutedState);
        await ChatClient.SetVoiceMuted(newMutedState);
    }

    protected async Task DisconnectVoice()
    {
        await LeaveVoiceChannel();
        StateHasChanged();
    }

    protected async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await SendMessage();
        }
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

    protected string GetChannelIcon(ChannelDto channel)
    {
        return channel.Type == ChannelType.Voice
            ? Icons.Material.Rounded.VolumeUp
            : Icons.Material.Rounded.Tag;
    }

    protected string GetChannelLabel(ChannelDto channel)
    {
        return channel.Type == ChannelType.Voice ? channel.Name : $"#{channel.Name}";
    }

    protected string GetMessageBody(ChatMessage message)
    {
        return message.Message.DeletedAt.HasValue ? "[deleted]" : message.Message.Content;
    }

    protected string GetParticipantInitial(VoiceParticipantDto participant)
    {
        var value = string.IsNullOrWhiteSpace(participant.Name) ? participant.Handle : participant.Name;
        return string.IsNullOrWhiteSpace(value) ? "?" : value[..1].ToUpperInvariant();
    }

    protected bool IsParticipantSpeaking(VoiceParticipantDto participant)
    {
        return activeSpeakerIds.Contains(participant.UserId);
    }

    protected void BeginEdit(ChatMessage message)
    {
        editingMessageId = message.Message.Id;
        editText = message.Message.Content;
    }

    protected void CancelEdit()
    {
        editingMessageId = null;
        editText = string.Empty;
    }

    protected async Task SaveEdit()
    {
        if (_isVoiceChannel || !editingMessageId.HasValue || _membershipRole is null)
        {
            return;
        }

        var content = editText.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await ChatClient.EditMessage(new EditMessageRequest(
            ChannelId,
            editingMessageId.Value,
            content));
    }

    protected async Task DeleteMessage(ChatMessage message)
    {
        if (_isVoiceChannel || _membershipRole is null)
        {
            return;
        }

        await ChatClient.DeleteMessage(new DeleteMessageRequest(
            ChannelId,
            message.Message.Id));
    }

    protected async Task CreateChannel()
    {
        if (_membershipRole != MembershipRole.Admin)
        {
            return;
        }

        var channelName = newChannelName.Trim();
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return;
        }

        var channel = await NodeApiClient.CreateChannel(new CreateChannelRequest(channelName, newChannelType));
        if (channel is null)
        {
            return;
        }

        newChannelName = string.Empty;
        newChannelType = ChannelType.Text;
        NavigationManager.NavigateTo($"/channels/{channel.Id}");
    }

    protected string GetChannelHref(ChannelDto channel)
    {
        return $"/channels/{channel.Id}";
    }

    protected bool CanEditMessage(ChatMessage message)
    {
        return !_isVoiceChannel && message.IsOwnMessage && !message.Message.DeletedAt.HasValue;
    }

    protected bool CanDeleteMessage(ChatMessage message)
    {
        if (_isVoiceChannel || message.Message.DeletedAt.HasValue)
        {
            return false;
        }

        return message.IsOwnMessage || _membershipRole == MembershipRole.Admin;
    }

    public async ValueTask DisposeAsync()
    {
        ChatClient.MessageReceived -= OnMessageReceived;
        ChatClient.MessageUpdated -= OnMessageUpdated;
        ChatClient.VoiceChannelStateReceived -= OnVoiceChannelStateReceived;
        ChatClient.Reconnected -= OnReconnected;
        VoiceClient.ActiveSpeakersChanged -= OnActiveSpeakersChanged;

        await LeaveVoiceChannel();
    }

    private void OnMessageReceived(MessageDto message)
    {
        if (_isVoiceChannel || message.ChannelId != ChannelId)
        {
            return;
        }

        MergeConfirmedMessage(message);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnMessageUpdated(MessageDto message)
    {
        if (_isVoiceChannel || message.ChannelId != ChannelId)
        {
            return;
        }

        MergeConfirmedMessage(message);
        if (editingMessageId == message.Id)
        {
            CancelEdit();
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnVoiceChannelStateReceived(VoiceChannelStateDto state)
    {
        if (state.ChannelId != ChannelId)
        {
            return;
        }

        voiceParticipants.Clear();
        voiceParticipants.AddRange(state.Participants);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnActiveSpeakersChanged(IReadOnlySet<Guid> speakerIds)
    {
        activeSpeakerIds = speakerIds.ToHashSet();
        _ = InvokeAsync(StateHasChanged);
    }

    private ChatMessage ToChatMessage(MessageDto message, MessageState state)
    {
        return new ChatMessage
        {
            Message = message,
            State = state,
            IsOwnMessage = AuthService.UserId.HasValue && message.AuthorId == AuthService.UserId.Value
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
        if (_isVoiceChannel)
        {
            var state = await NodeApiClient.GetVoiceChannelState(ChannelId);
            voiceParticipants.Clear();
            voiceParticipants.AddRange(state?.Participants ?? []);
            activeSpeakerIds.Clear();
            await InvokeAsync(StateHasChanged);
            return;
        }

        await RecoverMissingMessages();
        await InvokeAsync(StateHasChanged);
    }

    private void MergeConfirmedMessage(MessageDto message)
    {
        var existing = messages.FirstOrDefault(current => current.Message.Id == message.Id);
        if (message.DeletedAt.HasValue)
        {
            if (existing is not null)
            {
                messages.Remove(existing);
                SortMessages();
            }

            return;
        }

        if (existing is not null)
        {
            existing.Message = message;
            existing.State = MessageState.Confirmed;
            SortMessages();
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
            var missingMessages = await NodeApiClient.GetMessages(
                ChannelId,
                after: GetLastConfirmedMessageId(),
                limit: HistoryPageSize);
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
        _isAuthenticated = AuthService.IsAuthenticated;
        var node = await NodeApiClient.GetNode();
        _nodeName = node?.Name ?? "Relay";

        var availableChannels = await NodeApiClient.GetChannels();
        var channel = availableChannels.FirstOrDefault(current => current.Id == ChannelId)
            ?? throw new InvalidOperationException($"Channel '{ChannelId}' was not returned by the node API.");

        _channelName = channel.Name;
        channelType = channel.Type;
        channels.Clear();
        channels.AddRange(availableChannels);

        var membership = _isAuthenticated ? await NodeApiClient.GetMembership() : null;
        _membershipRole = membership?.Role;
    }

    private async Task LoadVoiceChannel()
    {
        if (VoiceClient.ActiveChannelId.HasValue && VoiceClient.ActiveChannelId.Value != ChannelId)
        {
            await LeaveVoiceChannel();
        }

        if (!_isConnectedToVoiceChannel)
        {
            var access = await NodeApiClient.GetVoiceChannelAccess(ChannelId)
                ?? throw new InvalidOperationException("The node did not return a LiveKit voice access token.");

            await ChatClient.JoinVoiceChannel(ChannelId);

            try
            {
                await VoiceClient.Join(ChannelId, access);
            }
            catch
            {
                await ChatClient.LeaveVoiceChannel();
                throw;
            }
        }

        var state = await NodeApiClient.GetVoiceChannelState(ChannelId);
        voiceParticipants.Clear();
        voiceParticipants.AddRange(state?.Participants ?? []);
    }

    private async Task LeaveVoiceChannel()
    {
        if (!VoiceClient.ActiveChannelId.HasValue)
        {
            voiceParticipants.Clear();
            activeSpeakerIds.Clear();
            return;
        }

        await ChatClient.LeaveVoiceChannel();
        await VoiceClient.Leave();
        voiceParticipants.Clear();
        activeSpeakerIds.Clear();
    }
}

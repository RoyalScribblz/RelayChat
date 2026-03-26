using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RelayChat.Client.Services;
using RelayChat.Node.Contracts;

namespace RelayChat.Client.Pages;

public partial class Chat : ComponentBase, IAsyncDisposable
{
    private const int HistoryPageSize = 100;
    private const string ComposerId = "chat-composer";
    private const string MessageScrollContainerId = "chat-message-scroll";
    private readonly List<ChatMessage> messages = [];
    private readonly List<ChannelDto> channels = [];
    private readonly List<MembershipDto> members = [];
    private readonly List<VoiceParticipantDto> voiceParticipants = [];
    private string messageText = string.Empty;
    private string editText = string.Empty;
    private string newChannelName = string.Empty;
    private Guid? joinedTextChannelId;
    private Guid? draggedChannelId;
    private Guid? nodeId;
    private Guid? editingMessageId;
    private ChannelType newChannelType = ChannelType.Text;
    private ChannelType currentChannelType = ChannelType.Text;
    private IJSObjectReference? chatModule;
    private DotNetObjectReference<Chat>? callbackReference;
    private bool composerRegistered;
    private bool shouldScrollMessages;

    [Parameter]
    public Guid? ChannelId { get; set; }

    [Inject]
    public required AuthService AuthService { get; init; }

    [Inject]
    public required ChatClient ChatClient { get; init; }

    [Inject]
    public required IJSRuntime JsRuntime { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required NodeApiClient NodeApiClient { get; init; }

    [Inject]
    public required VoiceClient VoiceClient { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    protected bool _isAuthenticated { get; private set; }
    protected MembershipRole? _membershipRole { get; private set; }
    protected string _nodeName { get; private set; } = "Relay";
    protected string _currentChannelName { get; private set; } = string.Empty;
    protected bool _hasServer => _membershipRole is not null;
    protected bool _isVoiceChannel => currentChannelType == ChannelType.Voice;
    protected bool _isTextChannel => currentChannelType == ChannelType.Text;
    protected bool _isConnectedToVoiceChannel => VoiceClient.ActiveChannelId == ChannelId;
    protected bool _isVoiceMuted => VoiceClient.IsMuted;
    protected bool _isCameraEnabled => VoiceClient.IsCameraEnabled;
    protected bool _isScreenShareEnabled => VoiceClient.IsScreenShareEnabled;
    protected bool _canCreateChannels => _membershipRole == MembershipRole.Admin;
    protected bool _canReorderChannels => _membershipRole == MembershipRole.Admin && channels.Count > 1;
    protected bool _hasChannels => channels.Count > 0;
    protected string _serverInitial => string.IsNullOrWhiteSpace(_nodeName) ? "R" : _nodeName[..1].ToUpperInvariant();
    protected IReadOnlyList<ChannelDto> _channels => channels;
    protected IReadOnlyList<MembershipDto> _members => members;
    protected IReadOnlyList<VoiceParticipantDto> _voiceParticipants => voiceParticipants;
    protected IReadOnlyList<ChatDayGroup> _chatDays => BuildChatDays();
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
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadShellContext();

        if (!_hasServer)
        {
            await LeaveVoiceChannel();
            await UnregisterComposer();
            joinedTextChannelId = null;
            messages.Clear();
            _currentChannelName = string.Empty;
            currentChannelType = ChannelType.Text;
            return;
        }

        var selectedChannel = await ResolveSelectedChannel();
        if (selectedChannel is null)
        {
            await LeaveVoiceChannel();
            await UnregisterComposer();
            messages.Clear();
            _currentChannelName = string.Empty;
            currentChannelType = ChannelType.Text;
            return;
        }

        if (ChannelId != selectedChannel.Id)
        {
            NavigationManager.NavigateTo(GetChannelHref(selectedChannel), replace: true);
            return;
        }

        _currentChannelName = selectedChannel.Name;
        currentChannelType = selectedChannel.Type;
        await PersistLastChannel(selectedChannel.Id);

        if (_isVoiceChannel)
        {
            await UnregisterComposer();
            messages.Clear();
            editingMessageId = null;
            editText = string.Empty;
            joinedTextChannelId = null;
            await LoadVoiceChannel(selectedChannel.Id);
            return;
        }

        if (VoiceClient.ActiveChannelId.HasValue)
        {
            await LeaveVoiceChannel();
        }

        await RegisterComposer();
        await LoadTextChannel(selectedChannel.Id);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            chatModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/chatPage.js");
        }

        if (shouldScrollMessages && chatModule is not null)
        {
            shouldScrollMessages = false;
            await chatModule.InvokeVoidAsync("scrollToBottom", MessageScrollContainerId);
        }
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
        await LoadShellContext();

        var selectedChannel = await ResolveSelectedChannel(preferTextChannel: true);
        if (selectedChannel is null)
        {
            StateHasChanged();
            return;
        }

        NavigationManager.NavigateTo(GetChannelHref(selectedChannel));
    }

    protected async Task SendMessage()
    {
        if (!_isTextChannel || !_isAuthenticated || _membershipRole is null || !AuthService.UserId.HasValue || !ChannelId.HasValue)
        {
            return;
        }

        var content = messageText.TrimEnd();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var pendingMessage = new ChatMessage
        {
            Message = new MessageDto(
                Guid.NewGuid(),
                ChannelId.Value,
                AuthService.UserId.Value,
                AuthService.Name ?? AuthService.Handle ?? "You",
                AuthService.Handle ?? "you",
                AuthService.AvatarUrl,
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
        shouldScrollMessages = true;
        await InvokeAsync(StateHasChanged);
        await FocusComposer();

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

    protected async Task ToggleCamera()
    {
        var newCameraState = !VoiceClient.IsCameraEnabled;
        await VoiceClient.SetCameraEnabled(newCameraState);
        await InvokeAsync(StateHasChanged);
    }

    protected async Task ToggleScreenShare()
    {
        var newScreenShareState = !VoiceClient.IsScreenShareEnabled;
        var result = await VoiceClient.SetScreenShareEnabled(newScreenShareState);
        if (result.FellBackToVideoOnly)
        {
            Snackbar.Add(
                "Audio capture was unavailable for the selected source. Sharing screen without audio instead. Browser tabs support audio more reliably than windows or full displays.",
                Severity.Warning);
        }

        await InvokeAsync(StateHasChanged);
    }

    protected async Task DisconnectVoice()
    {
        await LeaveVoiceChannel();

        var fallbackChannel = channels.FirstOrDefault(channel => channel.Type == ChannelType.Text) ?? channels.FirstOrDefault();
        if (fallbackChannel is not null)
        {
            NavigationManager.NavigateTo(GetChannelHref(fallbackChannel));
            return;
        }

        NavigationManager.NavigateTo("/");
    }

    protected void NoOp()
    {
    }

    protected string GetServerHref()
    {
        return ChannelId.HasValue ? $"/channels/{ChannelId.Value}" : "#";
    }

    protected bool IsCurrentChannel(ChannelDto channel)
    {
        return ChannelId.HasValue && channel.Id == ChannelId.Value;
    }

    protected string GetChannelHref(ChannelDto channel)
    {
        return $"/channels/{channel.Id}";
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

    protected string GetDisplayDate(DateOnly day)
    {
        return day.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy");
    }

    protected string GetMessageGroupInitial(ChatMessageGroup group)
    {
        var value = string.IsNullOrWhiteSpace(group.AuthorName) ? group.AuthorHandle : group.AuthorName;
        return string.IsNullOrWhiteSpace(value) ? "?" : value[..1].ToUpperInvariant();
    }

    protected string GetMemberInitial(MembershipDto member)
    {
        var value = string.IsNullOrWhiteSpace(member.Name) ? member.Handle : member.Name;
        return string.IsNullOrWhiteSpace(value) ? "?" : value[..1].ToUpperInvariant();
    }

    protected IEnumerable<IGrouping<MembershipRole, MembershipDto>> GetMemberGroups()
    {
        return members
            .OrderBy(member => member.Role)
            .ThenBy(member => member.Name)
            .ThenBy(member => member.Handle)
            .GroupBy(member => member.Role);
    }

    protected string GetMemberGroupLabel(MembershipRole role)
    {
        return role switch
        {
            MembershipRole.Admin => "Admins",
            MembershipRole.Member => "Members",
            _ => role.ToString()
        };
    }

    protected string GetMessageStateLabel(ChatMessage message)
    {
        return message.State switch
        {
            MessageState.Pending => "Sending",
            MessageState.Failed => "Failed",
            _ => string.Empty
        };
    }

    protected bool CanEditMessage(ChatMessage message)
    {
        return _isTextChannel && message.IsOwnMessage && !message.Message.DeletedAt.HasValue;
    }

    protected bool CanDeleteMessage(ChatMessage message)
    {
        if (!_isTextChannel || message.Message.DeletedAt.HasValue)
        {
            return false;
        }

        return message.IsOwnMessage || _membershipRole == MembershipRole.Admin;
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
        if (!_isTextChannel || !editingMessageId.HasValue || _membershipRole is null || !ChannelId.HasValue)
        {
            return;
        }

        var content = editText.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await ChatClient.EditMessage(new EditMessageRequest(
            ChannelId.Value,
            editingMessageId.Value,
            content));
    }

    protected async Task DeleteMessage(ChatMessage message)
    {
        if (!_isTextChannel || _membershipRole is null || !ChannelId.HasValue)
        {
            return;
        }

        await ChatClient.DeleteMessage(new DeleteMessageRequest(ChannelId.Value, message.Message.Id));
    }

    protected async Task CreateChannel()
    {
        if (!_canCreateChannels)
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
        channels.Clear();
        channels.AddRange((await NodeApiClient.GetChannels()).OrderBy(current => current.SortOrder));
        NavigationManager.NavigateTo(GetChannelHref(channel));
    }

    protected void BeginChannelDrag(Guid channelId)
    {
        if (!_canReorderChannels)
        {
            return;
        }

        draggedChannelId = channelId;
    }

    protected async Task DropChannel(Guid targetChannelId)
    {
        if (!_canReorderChannels || !draggedChannelId.HasValue || draggedChannelId.Value == targetChannelId)
        {
            draggedChannelId = null;
            return;
        }

        var orderedIds = channels.Select(channel => channel.Id).ToList();
        var sourceIndex = orderedIds.IndexOf(draggedChannelId.Value);
        var targetIndex = orderedIds.IndexOf(targetChannelId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            draggedChannelId = null;
            return;
        }

        orderedIds.RemoveAt(sourceIndex);
        orderedIds.Insert(targetIndex, draggedChannelId.Value);

        try
        {
            var updatedChannels = await NodeApiClient.ReorderChannels(orderedIds);
            channels.Clear();
            channels.AddRange(updatedChannels.OrderBy(channel => channel.SortOrder));
        }
        finally
        {
            draggedChannelId = null;
        }
    }

    [JSInvokable]
    public Task HandleComposerSubmit()
    {
        return SendMessage();
    }

    public async ValueTask DisposeAsync()
    {
        ChatClient.MessageReceived -= OnMessageReceived;
        ChatClient.MessageUpdated -= OnMessageUpdated;
        ChatClient.VoiceChannelStateReceived -= OnVoiceChannelStateReceived;
        ChatClient.Reconnected -= OnReconnected;

        await LeaveVoiceChannel();
        await UnregisterComposer();

        if (chatModule is not null)
        {
            await chatModule.DisposeAsync();
        }

        callbackReference?.Dispose();
    }

    private async Task LoadShellContext()
    {
        _isAuthenticated = AuthService.IsAuthenticated;

        var node = await NodeApiClient.GetNode();
        nodeId = node?.Id;
        _nodeName = node?.Name ?? "Relay";

        channels.Clear();
        channels.AddRange((await NodeApiClient.GetChannels()).OrderBy(channel => channel.SortOrder));

        var membership = _isAuthenticated ? await NodeApiClient.GetMembership() : null;
        _membershipRole = membership?.Role;

        members.Clear();
        if (_membershipRole is not null)
        {
            members.AddRange(await NodeApiClient.GetMembers());
        }
    }

    private async Task<ChannelDto?> ResolveSelectedChannel(bool preferTextChannel = false)
    {
        if (!_hasServer || channels.Count == 0)
        {
            return null;
        }

        if (!preferTextChannel && ChannelId.HasValue)
        {
            var routeChannel = channels.FirstOrDefault(channel => channel.Id == ChannelId.Value);
            if (routeChannel is not null)
            {
                return routeChannel;
            }
        }

        var rememberedChannelId = await GetRememberedChannel();
        if (!preferTextChannel && rememberedChannelId.HasValue)
        {
            var rememberedChannel = channels.FirstOrDefault(channel => channel.Id == rememberedChannelId.Value);
            if (rememberedChannel is not null)
            {
                return rememberedChannel;
            }
        }

        return channels.FirstOrDefault(channel => channel.Type == ChannelType.Text)
               ?? channels.FirstOrDefault();
    }

    private async Task<Guid?> GetRememberedChannel()
    {
        if (!nodeId.HasValue)
        {
            return null;
        }

        var storedValue = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", GetLastChannelStorageKey(nodeId.Value));
        return Guid.TryParse(storedValue, out var channelId) ? channelId : null;
    }

    private async Task PersistLastChannel(Guid channelId)
    {
        if (!nodeId.HasValue)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("localStorage.setItem", GetLastChannelStorageKey(nodeId.Value), channelId.ToString());
    }

    private static string GetLastChannelStorageKey(Guid currentNodeId)
    {
        return $"relaychat.node.{currentNodeId}:last-channel";
    }

    private async Task LoadTextChannel(Guid channelId)
    {
        if (joinedTextChannelId != channelId)
        {
            messages.Clear();
            messageText = string.Empty;
            editingMessageId = null;
            editText = string.Empty;
        }

        messages.Clear();
        var history = await NodeApiClient.GetMessages(channelId, limit: HistoryPageSize);
        foreach (var message in history)
        {
            MergeConfirmedMessage(message);
        }

        await ChatClient.JoinChannel(channelId);
        joinedTextChannelId = channelId;
        shouldScrollMessages = true;
    }

    private async Task LoadVoiceChannel(Guid channelId)
    {
        if (VoiceClient.ActiveChannelId.HasValue && VoiceClient.ActiveChannelId.Value != channelId)
        {
            await LeaveVoiceChannel();
        }

        if (!_isConnectedToVoiceChannel)
        {
            var access = await NodeApiClient.GetVoiceChannelAccess(channelId)
                ?? throw new InvalidOperationException("The node did not return a LiveKit voice access token.");

            await ChatClient.JoinVoiceChannel(channelId);

            try
            {
                await VoiceClient.Join(channelId, access);
            }
            catch
            {
                await ChatClient.LeaveVoiceChannel();
                throw;
            }
        }

        var state = await NodeApiClient.GetVoiceChannelState(channelId);
        voiceParticipants.Clear();
        voiceParticipants.AddRange(state?.Participants ?? []);
        await VoiceClient.SetVoiceParticipants(voiceParticipants);
    }

    private async Task LeaveVoiceChannel()
    {
        if (!VoiceClient.ActiveChannelId.HasValue)
        {
            voiceParticipants.Clear();
            return;
        }

        await ChatClient.LeaveVoiceChannel();
        await VoiceClient.Leave();
        voiceParticipants.Clear();
    }

    private async Task RegisterComposer()
    {
        chatModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/chatPage.js");
        if (composerRegistered)
        {
            return;
        }

        callbackReference ??= DotNetObjectReference.Create(this);
        await chatModule.InvokeVoidAsync("registerComposer", callbackReference, ComposerId);
        composerRegistered = true;
    }

    private async Task UnregisterComposer()
    {
        if (!composerRegistered || chatModule is null)
        {
            composerRegistered = false;
            return;
        }

        await chatModule.InvokeVoidAsync("unregisterComposer");
        composerRegistered = false;
    }

    private async Task FocusComposer()
    {
        if (chatModule is null)
        {
            return;
        }

        await chatModule.InvokeVoidAsync("focusComposer");
    }

    private void OnMessageReceived(MessageDto message)
    {
        if (!_isTextChannel || !ChannelId.HasValue || message.ChannelId != ChannelId.Value)
        {
            return;
        }

        MergeConfirmedMessage(message);
        shouldScrollMessages = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnMessageUpdated(MessageDto message)
    {
        if (!_isTextChannel || !ChannelId.HasValue || message.ChannelId != ChannelId.Value)
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
        if (!_isVoiceChannel || !ChannelId.HasValue || state.ChannelId != ChannelId.Value)
        {
            return;
        }

        voiceParticipants.Clear();
        voiceParticipants.AddRange(state.Participants);
        _ = InvokeAsync(async () =>
        {
            await VoiceClient.SetVoiceParticipants(voiceParticipants);
            StateHasChanged();
        });
    }

    private async Task OnReconnected()
    {
        if (_isVoiceChannel && ChannelId.HasValue)
        {
            var state = await NodeApiClient.GetVoiceChannelState(ChannelId.Value);
            voiceParticipants.Clear();
            voiceParticipants.AddRange(state?.Participants ?? []);
            await VoiceClient.SetVoiceParticipants(voiceParticipants);
            await InvokeAsync(StateHasChanged);
            return;
        }

        await RecoverMissingMessages();
        shouldScrollMessages = true;
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

    private async Task RecoverMissingMessages()
    {
        if (!_isTextChannel || !ChannelId.HasValue)
        {
            return;
        }

        while (true)
        {
            var missingMessages = await NodeApiClient.GetMessages(
                ChannelId.Value,
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

    private IReadOnlyList<ChatDayGroup> BuildChatDays()
    {
        var dayGroups = new List<ChatDayGroup>();
        foreach (var message in messages.OrderBy(current => current.Message.CreatedAt).ThenBy(current => current.Message.Id))
        {
            var day = DateOnly.FromDateTime(message.Message.CreatedAt.LocalDateTime);
            var dayGroup = dayGroups.LastOrDefault();
            if (dayGroup is null || dayGroup.Day != day)
            {
                dayGroup = new ChatDayGroup(day);
                dayGroups.Add(dayGroup);
            }

            var currentGroup = dayGroup.Groups.LastOrDefault();
            if (currentGroup is null || !CanGroup(currentGroup.Messages.Last(), message))
            {
                currentGroup = new ChatMessageGroup(
                    message.Message.AuthorId,
                    message.Message.AuthorName,
                    message.Message.AuthorHandle,
                    message.Message.AuthorAvatarUrl,
                    message.Message.CreatedAt);
                dayGroup.Groups.Add(currentGroup);
            }

            currentGroup.Messages.Add(message);
        }

        return dayGroups;
    }

    private static bool CanGroup(ChatMessage previous, ChatMessage current)
    {
        return previous.Message.AuthorId == current.Message.AuthorId &&
               DateOnly.FromDateTime(previous.Message.CreatedAt.LocalDateTime) ==
               DateOnly.FromDateTime(current.Message.CreatedAt.LocalDateTime) &&
               current.Message.CreatedAt - previous.Message.CreatedAt <= TimeSpan.FromMinutes(1);
    }

    protected sealed class ChatDayGroup(DateOnly day)
    {
        public DateOnly Day { get; } = day;
        public List<ChatMessageGroup> Groups { get; } = [];
    }

    protected sealed class ChatMessageGroup(
        Guid authorId,
        string authorName,
        string authorHandle,
        string? authorAvatarUrl,
        DateTimeOffset startedAt)
    {
        public Guid AuthorId { get; } = authorId;
        public string AuthorName { get; } = authorName;
        public string AuthorHandle { get; } = authorHandle;
        public string? AuthorAvatarUrl { get; } = authorAvatarUrl;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public List<ChatMessage> Messages { get; } = [];
    }
}

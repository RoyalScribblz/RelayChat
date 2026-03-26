using RelayChat.Node.Contracts;

namespace RelayChat.Client;

public sealed class ChatMessage
{
    public required MessageDto Message { get; set; }
    public required MessageState State { get; set; }
    public required bool IsOwnMessage { get; init; }
}

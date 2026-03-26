namespace RelayChat.Node.Database;

public sealed class Channel
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
}

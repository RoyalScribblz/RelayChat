namespace RelayChat.Node.Database;

public sealed class Channel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

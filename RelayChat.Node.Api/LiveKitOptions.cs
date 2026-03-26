namespace RelayChat.Node.Api;

public sealed class LiveKitOptions
{
    public string ServerUrl { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}

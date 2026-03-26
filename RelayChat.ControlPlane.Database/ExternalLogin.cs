namespace RelayChat.ControlPlane.Database;

public sealed class ExternalLogin
{
    public string Provider { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}

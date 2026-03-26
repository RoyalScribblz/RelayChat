using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public sealed record ServerDto(Guid Id, string Name)
{
    public static ServerDto FromServer(Server server)
    {
        return new ServerDto(server.Id, server.Name);
    }
}

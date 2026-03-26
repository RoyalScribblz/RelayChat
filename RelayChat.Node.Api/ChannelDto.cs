using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public sealed record ChannelDto(Guid Id, Guid ServerId, string Name)
{
    public static ChannelDto FromChannel(Channel channel)
    {
        return new ChannelDto(channel.Id, channel.ServerId, channel.Name);
    }
}

using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public static class ContractMappings
{
    public static ChannelDto ToDto(this Channel channel)
    {
        return new ChannelDto(channel.Id, channel.ServerId, channel.Name);
    }

    public static MessageDto ToDto(this Message message)
    {
        return new MessageDto(
            message.Id,
            message.ChannelId,
            message.AuthorId,
            message.Content,
            message.CreatedAt,
            message.ClientMessageId,
            message.EditedAt,
            message.DeletedAt);
    }

    public static ServerDto ToDto(this Server server)
    {
        return new ServerDto(server.Id, server.Name);
    }

    public static ServerMembershipDto ToDto(this ServerMembership membership)
    {
        return new ServerMembershipDto(membership.ServerId, membership.UserId, membership.Role);
    }
}

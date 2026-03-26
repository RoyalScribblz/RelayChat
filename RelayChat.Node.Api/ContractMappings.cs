using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public static class ContractMappings
{
    public static ChannelDto ToDto(this Channel channel)
    {
        return new ChannelDto(channel.Id, channel.Name);
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

    public static MembershipDto ToDto(this Membership membership)
    {
        return new MembershipDto(membership.UserId, membership.Role);
    }

    public static NodeDto ToDto(this NodeState nodeState)
    {
        return new NodeDto(nodeState.Id, nodeState.Name);
    }
}

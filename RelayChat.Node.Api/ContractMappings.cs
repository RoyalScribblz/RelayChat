using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

namespace RelayChat.Node.Api;

public static class ContractMappings
{
    public static ChannelDto ToDto(this Channel channel)
    {
        return new ChannelDto(channel.Id, channel.Name, channel.Type, channel.SortOrder);
    }

    public static MessageDto ToDto(this Message message, Membership? author)
    {
        return new MessageDto(
            message.Id,
            message.ChannelId,
            message.AuthorId,
            author?.Name ?? message.AuthorId.ToString(),
            author?.Handle ?? message.AuthorId.ToString("N"),
            author?.AvatarUrl,
            message.Content,
            message.CreatedAt,
            message.ClientMessageId,
            message.EditedAt,
            message.DeletedAt);
    }

    public static MembershipDto ToDto(this Membership membership)
    {
        return new MembershipDto(
            membership.UserId,
            membership.Role,
            membership.Name,
            membership.Handle,
            membership.AvatarUrl);
    }

    public static NodeDto ToDto(this NodeState nodeState)
    {
        return new NodeDto(nodeState.Id, nodeState.Name);
    }
}

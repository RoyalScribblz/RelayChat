using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Contracts;

namespace RelayChat.Node.Database;

public sealed class ServerMembershipRepository(NodeDbContext dbContext)
{
    public async Task<ServerMembership?> Get(Guid serverId, Guid userId, CancellationToken ct = default)
    {
        return await dbContext.ServerMemberships
            .SingleOrDefaultAsync(
                membership => membership.ServerId == serverId && membership.UserId == userId,
                ct);
    }

    public async Task<ServerMembership> Add(Guid serverId, Guid userId, ServerMembershipRole role, CancellationToken ct = default)
    {
        var membership = new ServerMembership
        {
            ServerId = serverId,
            UserId = userId,
            Role = role
        };

        await dbContext.ServerMemberships.AddAsync(membership, ct);
        await dbContext.SaveChangesAsync(ct);
        return membership;
    }
}

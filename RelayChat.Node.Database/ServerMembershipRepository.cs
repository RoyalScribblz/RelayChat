using Microsoft.EntityFrameworkCore;

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

    public async Task<ServerMembership> GetOrCreate(Guid serverId, Guid userId, CancellationToken ct = default)
    {
        var membership = await Get(serverId, userId, ct);
        if (membership is not null)
        {
            return membership;
        }

        var role = await dbContext.ServerMemberships.AnyAsync(existing => existing.ServerId == serverId, ct)
            ? ServerMembershipRole.Member
            : ServerMembershipRole.Admin;

        membership = new ServerMembership
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

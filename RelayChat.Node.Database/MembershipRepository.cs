using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Contracts;

namespace RelayChat.Node.Database;

public sealed class MembershipRepository(NodeDbContext dbContext)
{
    public Task<Membership?> Get(Guid userId, CancellationToken ct = default)
    {
        return dbContext.Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, ct);
    }

    public async Task<Membership> Add(Guid userId, MembershipRole role, CancellationToken ct = default)
    {
        var membership = new Membership
        {
            UserId = userId,
            Role = role
        };

        await dbContext.Memberships.AddAsync(membership, ct);
        await dbContext.SaveChangesAsync(ct);
        return membership;
    }

    public Task<bool> Any(CancellationToken ct = default)
    {
        return dbContext.Memberships.AnyAsync(ct);
    }
}

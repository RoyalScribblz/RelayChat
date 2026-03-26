using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Contracts;

namespace RelayChat.Node.Database;

public sealed class MembershipRepository(NodeDbContext dbContext)
{
    public Task<Membership?> Get(Guid userId, CancellationToken ct = default)
    {
        return dbContext.Memberships
            .AsNoTracking()
            .SingleOrDefaultAsync(membership => membership.UserId == userId, ct);
    }

    public Task<List<Membership>> GetAll(CancellationToken ct = default)
    {
        return dbContext.Memberships
            .AsNoTracking()
            .OrderBy(membership => membership.Role)
            .ThenBy(membership => membership.Name)
            .ThenBy(membership => membership.Handle)
            .ToListAsync(ct);
    }

    public Task<List<Membership>> GetByUserIds(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        return dbContext.Memberships
            .AsNoTracking()
            .Where(membership => userIds.Contains(membership.UserId))
            .ToListAsync(ct);
    }

    public async Task<Membership> Add(
        Guid userId,
        MembershipRole role,
        string name,
        string handle,
        string? avatarUrl,
        CancellationToken ct = default)
    {
        var membership = new Membership
        {
            UserId = userId,
            Role = role,
            Name = name,
            Handle = handle,
            AvatarUrl = avatarUrl
        };

        await dbContext.Memberships.AddAsync(membership, ct);
        await dbContext.SaveChangesAsync(ct);
        return membership;
    }

    public Task<bool> Any(CancellationToken ct = default)
    {
        return dbContext.Memberships.AnyAsync(ct);
    }

    public async Task SyncProfile(Guid userId, string name, string handle, string? avatarUrl, CancellationToken ct = default)
    {
        var membership = await dbContext.Memberships.SingleOrDefaultAsync(current => current.UserId == userId, ct);
        if (membership is null)
        {
            return;
        }

        if (membership.Name == name && membership.Handle == handle && membership.AvatarUrl == avatarUrl)
        {
            return;
        }

        membership.Name = name;
        membership.Handle = handle;
        membership.AvatarUrl = avatarUrl;
        await dbContext.SaveChangesAsync(ct);
    }
}

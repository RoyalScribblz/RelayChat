using Microsoft.EntityFrameworkCore;

namespace RelayChat.ControlPlane.Database;

public sealed class UserRepository(ControlPlaneDbContext dbContext)
{
    public Task<User?> Get(Guid userId, CancellationToken ct = default)
    {
        return dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, ct);
    }

    public Task<User?> GetByExternalLogin(string provider, string subject, CancellationToken ct = default)
    {
        return (
            from login in dbContext.ExternalLogins
            join user in dbContext.Users on login.UserId equals user.Id
            where login.Provider == provider && login.Subject == subject
            select user)
            .SingleOrDefaultAsync(ct);
    }

    public Task<bool> HandleExists(string normalizedHandle, Guid? excludingUserId = null, CancellationToken ct = default)
    {
        return dbContext.Users.AnyAsync(
            user => user.HandleNormalized == normalizedHandle && (!excludingUserId.HasValue || user.Id != excludingUserId.Value),
            ct);
    }

    public async Task Add(User user, ExternalLogin externalLogin, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
        await dbContext.ExternalLogins.AddAsync(externalLogin, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task SaveChanges(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class ServerRepository(NodeDbContext dbContext)
{
    public async Task Add(Server server, CancellationToken ct = default)
    {
        await dbContext.Servers.AddAsync(server, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<Server?> Get(Guid serverId, CancellationToken ct = default)
    {
        return dbContext.Servers
            .AsNoTracking()
            .SingleOrDefaultAsync(server => server.Id == serverId, ct);
    }

    public Task<List<Server>> GetAll(CancellationToken ct = default)
    {
        return dbContext.Servers
            .AsNoTracking()
            .OrderBy(server => server.Name)
            .ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class ServerRepository(NodeDbContext dbContext)
{
    public Task<List<Server>> GetAll(CancellationToken ct = default)
    {
        return dbContext.Servers
            .AsNoTracking()
            .OrderBy(server => server.Name)
            .ToListAsync(ct);
    }
}

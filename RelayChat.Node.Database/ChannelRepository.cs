using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class ChannelRepository(NodeDbContext dbContext)
{
    public Task<List<Channel>> GetByServer(Guid serverId, CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .Where(channel => channel.ServerId == serverId)
            .OrderBy(channel => channel.Name)
            .ToListAsync(ct);
    }
}

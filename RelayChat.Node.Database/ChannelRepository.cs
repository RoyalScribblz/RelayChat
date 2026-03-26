using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class ChannelRepository(NodeDbContext dbContext)
{
    public async Task Add(Channel channel, CancellationToken ct = default)
    {
        await dbContext.Channels.AddAsync(channel, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<Channel?> Get(Guid channelId, CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .SingleOrDefaultAsync(channel => channel.Id == channelId, ct);
    }

    public Task<Channel?> Get(Guid serverId, Guid channelId, CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .SingleOrDefaultAsync(
                channel => channel.ServerId == serverId && channel.Id == channelId,
                ct);
    }

    public Task<List<Channel>> GetByServer(Guid serverId, CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .Where(channel => channel.ServerId == serverId)
            .OrderBy(channel => channel.Name)
            .ToListAsync(ct);
    }
}

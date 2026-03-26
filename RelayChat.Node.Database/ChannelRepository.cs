using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class ChannelRepository(NodeDbContext dbContext)
{
    public async Task Add(Channel channel, CancellationToken ct = default)
    {
        channel.SortOrder = await GetNextSortOrder(ct);
        await dbContext.Channels.AddAsync(channel, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<Channel?> Get(Guid channelId, CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .SingleOrDefaultAsync(channel => channel.Id == channelId, ct);
    }

    public Task<List<Channel>> GetAll(CancellationToken ct = default)
    {
        return dbContext.Channels
            .AsNoTracking()
            .OrderBy(channel => channel.SortOrder)
            .ThenBy(channel => channel.Id)
            .ToListAsync(ct);
    }

    public async Task Reorder(IReadOnlyList<Guid> channelIds, CancellationToken ct = default)
    {
        var channels = await dbContext.Channels
            .OrderBy(channel => channel.SortOrder)
            .ThenBy(channel => channel.Id)
            .ToListAsync(ct);

        if (channels.Count != channelIds.Count || channels.Any(channel => !channelIds.Contains(channel.Id)))
        {
            throw new InvalidOperationException("The supplied channel ordering does not match the existing channels.");
        }

        for (var index = 0; index < channelIds.Count; index++)
        {
            var channel = channels.Single(current => current.Id == channelIds[index]);
            channel.SortOrder = index;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<int> GetNextSortOrder(CancellationToken ct)
    {
        var lastSortOrder = await dbContext.Channels
            .Select(channel => (int?)channel.SortOrder)
            .MaxAsync(ct);
        return (lastSortOrder ?? -1) + 1;
    }
}

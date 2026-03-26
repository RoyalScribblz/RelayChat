using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class MessageRepository(NodeDbContext dbContext)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 200;

    public async Task Add(Message message, CancellationToken ct = default)
    {
        await dbContext.Messages.AddAsync(message, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<Message?> Get(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        return dbContext.Messages
            .SingleOrDefaultAsync(
                message => message.ChannelId == channelId && message.Id == messageId,
                ct);
    }

    public Task SaveChanges(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<Message>> GetByChannel(
        Guid channelId,
        Guid? before = null,
        Guid? after = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var query = dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ChannelId == channelId);
        var pageSize = NormalizeLimit(limit);

        if (after.HasValue)
        {
            var anchor = await query
                .Where(message => message.Id == after.Value)
                .Select(message => new { message.Id, message.CreatedAt })
                .SingleOrDefaultAsync(ct);

            if (anchor is not null)
            {
                query = query.Where(message =>
                    message.CreatedAt > anchor.CreatedAt ||
                    (message.CreatedAt == anchor.CreatedAt && message.Id.CompareTo(anchor.Id) > 0));
            }

            return await query
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        if (before.HasValue)
        {
            var anchor = await query
                .Where(message => message.Id == before.Value)
                .Select(message => new { message.Id, message.CreatedAt })
                .SingleOrDefaultAsync(ct);

            if (anchor is not null)
            {
                query = query.Where(message =>
                    message.CreatedAt < anchor.CreatedAt ||
                    (message.CreatedAt == anchor.CreatedAt && message.Id.CompareTo(anchor.Id) < 0));
            }

            var page = await query
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Take(pageSize)
                .ToListAsync(ct);

            page.Reverse();
            return page;
        }

        return await query
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    private static int NormalizeLimit(int? limit)
    {
        if (!limit.HasValue || limit.Value <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }
}

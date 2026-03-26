using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Api;

public sealed class MessageRepository(NodeDbContext dbContext)
{
    public async Task Add(Message message, CancellationToken ct = default)
    {
        await dbContext.Messages.AddAsync(message, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<List<Message>> GetByChannel(Guid channelId, CancellationToken ct = default)
    {
        return dbContext.Messages
            .Where(message => message.ChannelId == channelId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(ct);
    }
}

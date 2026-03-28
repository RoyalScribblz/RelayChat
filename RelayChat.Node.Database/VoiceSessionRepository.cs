using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class VoiceSessionRepository(NodeDbContext dbContext)
{
    public async Task Clear(CancellationToken ct = default)
    {
        await dbContext.VoiceSessions.ExecuteDeleteAsync(ct);
    }

    public Task<VoiceSession?> Get(Guid userId, CancellationToken ct = default)
    {
        return dbContext.VoiceSessions.SingleOrDefaultAsync(session => session.UserId == userId, ct);
    }

    public Task<List<VoiceSession>> GetByChannel(Guid channelId, CancellationToken ct = default)
    {
        return dbContext.VoiceSessions
            .AsNoTracking()
            .Where(session => session.ChannelId == channelId)
            .OrderBy(session => session.Name)
            .ThenBy(session => session.UserId)
            .ToListAsync(ct);
    }

    public async Task Upsert(VoiceSession session, CancellationToken ct = default)
    {
        var existing = await dbContext.VoiceSessions.SingleOrDefaultAsync(current => current.UserId == session.UserId, ct);
        if (existing is null)
        {
            await dbContext.VoiceSessions.AddAsync(session, ct);
        }
        else
        {
            existing.ChannelId = session.ChannelId;
            existing.ConnectionId = session.ConnectionId;
            existing.Name = session.Name;
            existing.Handle = session.Handle;
            existing.AvatarUrl = session.AvatarUrl;
            existing.IsMuted = session.IsMuted;
            existing.IsDeafened = session.IsDeafened;
            existing.JoinedAt = session.JoinedAt;
            existing.UpdatedAt = session.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task Remove(Guid userId, CancellationToken ct = default)
    {
        var existing = await dbContext.VoiceSessions.SingleOrDefaultAsync(session => session.UserId == userId, ct);
        if (existing is null)
        {
            return;
        }

        dbContext.VoiceSessions.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateMuted(Guid userId, bool isMuted, CancellationToken ct = default)
    {
        var existing = await dbContext.VoiceSessions.SingleOrDefaultAsync(session => session.UserId == userId, ct);
        if (existing is null)
        {
            return;
        }

        existing.IsMuted = isMuted;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateDeafened(Guid userId, bool isDeafened, CancellationToken ct = default)
    {
        var existing = await dbContext.VoiceSessions.SingleOrDefaultAsync(session => session.UserId == userId, ct);
        if (existing is null)
        {
            return;
        }

        existing.IsDeafened = isDeafened;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Guid?> SyncProfile(Guid userId, string name, string handle, string? avatarUrl, CancellationToken ct = default)
    {
        var existing = await dbContext.VoiceSessions.SingleOrDefaultAsync(session => session.UserId == userId, ct);
        if (existing is null)
        {
            return null;
        }

        existing.Name = name;
        existing.Handle = handle;
        existing.AvatarUrl = avatarUrl;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return existing.ChannelId;
    }
}

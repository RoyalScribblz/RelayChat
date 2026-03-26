using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class NodeStateRepository(NodeDbContext dbContext)
{
    public Task<NodeState?> Get(CancellationToken ct = default)
    {
        return dbContext.NodeStates.AsNoTracking().SingleOrDefaultAsync(ct);
    }
}

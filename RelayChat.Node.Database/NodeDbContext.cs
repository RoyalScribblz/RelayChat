using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class NodeDbContext(DbContextOptions<NodeDbContext> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
}

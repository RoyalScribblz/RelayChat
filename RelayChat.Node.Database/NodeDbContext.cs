using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class NodeDbContext(DbContextOptions<NodeDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<NodeState> NodeStates => Set<NodeState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Membership>().HasKey(membership => membership.UserId);

        modelBuilder.Entity<NodeState>().HasData(new NodeState
        {
            Id = NodeSeedData.DefaultNodeId,
            Name = "Relay"
        });

        modelBuilder.Entity<Channel>().HasData(new Channel
        {
            Id = NodeSeedData.DefaultChannelId,
            Name = "general"
        });
    }
}

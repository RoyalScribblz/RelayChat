using Microsoft.EntityFrameworkCore;

namespace RelayChat.Node.Database;

public sealed class NodeDbContext(DbContextOptions<NodeDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ServerMembership> ServerMemberships => Set<ServerMembership>();
    public DbSet<Server> Servers => Set<Server>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerMembership>().HasKey(membership => new { membership.ServerId, membership.UserId });

        modelBuilder.Entity<Server>().HasData(new Server
        {
            Id = NodeSeedData.DefaultServerId,
            Name = "Relay"
        });

        modelBuilder.Entity<Channel>().HasData(new Channel
        {
            Id = NodeSeedData.DefaultChannelId,
            ServerId = NodeSeedData.DefaultServerId,
            Name = "general"
        });
    }
}

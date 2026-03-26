using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Contracts;

namespace RelayChat.Node.Database;

public sealed class NodeDbContext(DbContextOptions<NodeDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<NodeState> NodeStates => Set<NodeState>();
    public DbSet<VoiceSession> VoiceSessions => Set<VoiceSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Membership>().HasKey(membership => membership.UserId);
        modelBuilder.Entity<VoiceSession>().HasKey(session => session.UserId);

        modelBuilder.Entity<NodeState>().HasData(new NodeState
        {
            Id = NodeSeedData.DefaultNodeId,
            Name = "Relay"
        });

        modelBuilder.Entity<Channel>().HasData(new Channel
        {
            Id = NodeSeedData.DefaultTextChannelId,
            Name = "general",
            Type = ChannelType.Text
        });

        modelBuilder.Entity<Channel>().HasData(new Channel
        {
            Id = NodeSeedData.DefaultVoiceChannelId,
            Name = "General Voice",
            Type = ChannelType.Voice
        });
    }
}

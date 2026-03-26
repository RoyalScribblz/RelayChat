using Microsoft.EntityFrameworkCore;

namespace RelayChat.ControlPlane.Database;

public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.HandleNormalized).IsUnique();
            entity.Property(user => user.Name).HasMaxLength(64);
            entity.Property(user => user.Handle).HasMaxLength(32);
            entity.Property(user => user.HandleNormalized).HasMaxLength(32);
            entity.Property(user => user.AvatarUrl).HasMaxLength(2048);
            entity.Property(user => user.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(login => new { login.Provider, login.Subject });
            entity.HasIndex(login => login.UserId).IsUnique();
            entity.Property(login => login.Provider).HasMaxLength(64);
            entity.Property(login => login.Subject).HasMaxLength(256);
        });
    }
}

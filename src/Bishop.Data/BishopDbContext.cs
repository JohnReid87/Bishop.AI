using Bishop.Core;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Data;

public sealed class BishopDbContext : DbContext
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Lane> Lanes => Set<Lane>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<CardTag> CardTags => Set<CardTag>();

    public BishopDbContext(DbContextOptions<BishopDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BishopDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Workspace>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        foreach (var entry in ChangeTracker.Entries<Card>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        foreach (var entry in ChangeTracker.Entries<Tag>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

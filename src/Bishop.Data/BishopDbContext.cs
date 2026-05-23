using Bishop.Core;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Data;

public sealed class BishopDbContext : DbContext
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<WorkspaceSkillRun> WorkspaceSkillRuns => Set<WorkspaceSkillRun>();

    public BishopDbContext(DbContextOptions<BishopDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BishopDbContext).Assembly);

        // Guids are stored as TEXT in SQLite. Different write paths (EF, manual SQL,
        // older provider versions) have produced rows with inconsistent casing, which
        // breaks FK matching under SQLite's default BINARY collation.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                    property.SetCollation("NOCASE");
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

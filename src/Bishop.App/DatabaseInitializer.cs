using Bishop.App.Tags;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IDefaultTagSeeder _tagSeeder;

    public DatabaseInitializer(IDbContextFactory<BishopDbContext> dbFactory, IDefaultTagSeeder tagSeeder)
    {
        _dbFactory = dbFactory;
        _tagSeeder = tagSeeder;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await _tagSeeder.EnsureAllAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (IsStampCurrent(db))
            return;

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
            await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await WriteStampAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsStampCurrent(BishopDbContext db)
    {
        var stampFile = StampFilePath();
        if (!File.Exists(stampFile)) return false;
        var latest = db.Database.GetMigrations().LastOrDefault();
        return latest is not null && File.ReadAllText(stampFile).Trim() == latest;
    }

    private static async Task WriteStampAsync(BishopDbContext db, CancellationToken cancellationToken)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        var latest = applied.LastOrDefault();
        if (latest is null) return;
        await File.WriteAllTextAsync(StampFilePath(), latest, cancellationToken);
    }

    private static string StampFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI",
            "migration_stamp");
}

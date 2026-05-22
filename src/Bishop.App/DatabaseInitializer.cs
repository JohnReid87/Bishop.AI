using Bishop.App.Tags;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private readonly BishopDbContext _db;
    private readonly IDefaultTagSeeder _tagSeeder;

    public DatabaseInitializer(BishopDbContext db, IDefaultTagSeeder tagSeeder)
    {
        _db = db;
        _tagSeeder = tagSeeder;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await _tagSeeder.EnsureAllAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (IsStampCurrent())
            return;

        var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
            await _db.Database.MigrateAsync(cancellationToken);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await WriteStampAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsStampCurrent()
    {
        var stampFile = StampFilePath();
        if (!File.Exists(stampFile)) return false;
        var latest = _db.Database.GetMigrations().LastOrDefault();
        return latest is not null && File.ReadAllText(stampFile).Trim() == latest;
    }

    private async Task WriteStampAsync(CancellationToken cancellationToken)
    {
        var applied = await _db.Database.GetAppliedMigrationsAsync(cancellationToken);
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

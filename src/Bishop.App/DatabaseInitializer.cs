using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private readonly BishopDbContext _db;

    public DatabaseInitializer(BishopDbContext db) => _db = db;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _db.Database.MigrateAsync(cancellationToken);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

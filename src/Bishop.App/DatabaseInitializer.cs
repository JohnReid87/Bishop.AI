using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private readonly BishopDbContext _db;

    public DatabaseInitializer(BishopDbContext db) => _db = db;

    public Task StartAsync(CancellationToken cancellationToken)
        => _db.Database.MigrateAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

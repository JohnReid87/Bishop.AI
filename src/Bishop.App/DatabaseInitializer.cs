using System.Security.Cryptography;
using System.Text;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private static readonly TimeSpan MutexAcquireTimeout = TimeSpan.FromSeconds(60);

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly string _stampPath;
    private readonly string _mutexName;

    public DatabaseInitializer(IDbContextFactory<BishopDbContext> dbFactory, string stampPath)
    {
        _dbFactory = dbFactory;
        _stampPath = stampPath;
        _mutexName = BuildMutexName(stampPath);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureSchema(cancellationToken);
        return Task.CompletedTask;
    }

    private void EnsureSchema(CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(initiallyOwned: false, _mutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(MutexAcquireTimeout);
            }
            catch (AbandonedMutexException)
            {
                // A peer process died holding the mutex. The stamp is re-checked
                // inside the critical section, so it is safe to continue.
                acquired = true;
            }

            if (!acquired)
                throw new TimeoutException(
                    $"Failed to acquire migration mutex '{_mutexName}' within {MutexAcquireTimeout.TotalSeconds:N0}s.");

            cancellationToken.ThrowIfCancellationRequested();

            using var db = _dbFactory.CreateDbContext();

            if (IsStampCurrent(db))
                return;

            var pending = db.Database.GetPendingMigrations();
            if (pending.Any())
                db.Database.Migrate();
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            WriteStamp(db);
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsStampCurrent(BishopDbContext db)
    {
        if (!File.Exists(_stampPath)) return false;
        var latest = db.Database.GetMigrations().LastOrDefault();
        return latest is not null && File.ReadAllText(_stampPath).Trim() == latest;
    }

    private void WriteStamp(BishopDbContext db)
    {
        var applied = db.Database.GetAppliedMigrations();
        var latest = applied.LastOrDefault();
        if (latest is null) return;
        File.WriteAllText(_stampPath, latest);
    }

    internal static string BuildMutexName(string stampPath)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(stampPath));
        var hex = Convert.ToHexString(hash);
        return $"Local\\Bishop.AI.Migrations.{hex}";
    }
}

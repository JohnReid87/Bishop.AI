using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

internal sealed class DatabaseInitializer : IHostedService
{
    private static readonly TimeSpan MutexAcquireTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly string _mutexName;

    public DatabaseInitializer(IDbContextFactory<BishopDbContext> dbFactory, string dbConnectionString)
    {
        _dbFactory = dbFactory;
        _mutexName = BuildMutexName(dbConnectionString);
    }

    // Task.Run keeps the UI/host thread free while the blocking WaitOne runs on the thread pool.
    // EnsureSchema must remain synchronous so WaitOne and ReleaseMutex execute on the same thread
    // (Mutex has thread affinity; await would break that invariant).
    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Run(() => EnsureSchema(cancellationToken), CancellationToken.None);

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
                // A peer process died holding the mutex. __EFMigrationsHistory is still
                // authoritative inside the critical section, so it is safe to continue.
                acquired = true;
            }

            if (!acquired)
                throw new TimeoutException(
                    $"Failed to acquire migration mutex '{_mutexName}' within {MutexAcquireTimeout.TotalSeconds:N0}s.");

            // Link a per-migration timeout to the host token so a stuck holder is
            // bounded — whichever fires first cancels the in-flight EF operations.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(MigrationTimeout);
            var token = cts.Token;

            token.ThrowIfCancellationRequested();

            using var db = _dbFactory.CreateDbContext();
            db.Database.SetCommandTimeout((int)MigrationTimeout.TotalSeconds);

            var pending = db.Database.GetPendingMigrations();
            if (pending.Any())
            {
                ClearStaleMigrationLock(db);
                db.Database.MigrateAsync(token).GetAwaiter().GetResult();
            }
            db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", token).GetAwaiter().GetResult();
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // EF Core's SqliteHistoryRepository inserts a row into __EFMigrationsLock before
    // applying migrations and deletes it afterwards. If a previous run was cancelled
    // (MigrationTimeout above) or the process was killed after the lock row was inserted
    // but before it was released, the row is orphaned — and every later startup then
    // spins in AcquireDatabaseLockAsync until the token cancels, bricking the app with a
    // "task was canceled" exception. The named Mutex already serialises migrations across
    // all Bishop processes, so any surviving lock row is necessarily stale: clear it before
    // migrating so the orphaned-lock state is self-healing rather than fatal.
    private static void ClearStaleMigrationLock(BishopDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var exists = connection.CreateCommand();
        exists.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name='__EFMigrationsLock';";
        if (exists.ExecuteScalar() is null)
            return;

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM \"__EFMigrationsLock\";";
        delete.ExecuteNonQuery();
    }

    internal static string BuildMutexName(string dbConnectionString)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dbConnectionString));
        var hex = Convert.ToHexString(hash);
        return $"Local\\Bishop.AI.Migrations.{hex}";
    }
}

using Bishop.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests;

public sealed class DbFixture : IDisposable
{
    private readonly DbContextOptions<BishopDbContext> _options;

    public SqliteConnection Connection { get; }
    public BishopDbContext Db { get; }
    public IDbContextFactory<BishopDbContext> Factory { get; }

    public DbFixture()
    {
        // Named shared-cache in-memory database: unique per fixture, persists for
        // the lifetime of this connection, isolated from other DbFixture instances.
        var dbName = Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        Connection = new SqliteConnection(connectionString);
        Connection.Open();

        _options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(new SqliteForeignKeyInterceptor())
            .Options;

        Db = new BishopDbContext(_options);
        Db.Database.EnsureCreated();

        Factory = new SharedCacheDbContextFactory(_options);
    }

    public void Dispose()
    {
        Db.Dispose();
        Connection.Dispose();
    }

    private sealed class SharedCacheDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;

        public SharedCacheDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;

        public BishopDbContext CreateDbContext() => new(_options);
    }
}

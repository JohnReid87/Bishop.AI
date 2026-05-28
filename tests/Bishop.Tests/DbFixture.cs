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
            .EnableServiceProviderCaching(false)
            .Options;

        Db = new BishopDbContext(_options);
        Db.Database.EnsureCreated();

        Factory = new SharedCacheDbContextFactory(_options);
    }

    /// <summary>
    /// Inserts a workspace row via raw ADO.NET (bypassing EF) and returns its ID.
    /// Call from test class constructors — each call creates a distinct workspace.
    /// </summary>
    public Guid SeedWorkspace()
    {
        var wsId = Guid.NewGuid();
        using var cmd = Connection.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("O");
        cmd.CommandText = @"INSERT INTO Workspaces (Id, Name, Path, Position, NextCardNumber, IsRemoved, CreatedAt, UpdatedAt)
                            VALUES (@id, @name, @path, 0, 1, 0, @now, @now)";
        cmd.Parameters.AddWithValue("@id", wsId.ToString());
        cmd.Parameters.AddWithValue("@name", "test-ws-" + wsId.ToString("N")[..8]);
        cmd.Parameters.AddWithValue("@path", @"C:\test-" + wsId.ToString("N")[..8]);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();
        return wsId;
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

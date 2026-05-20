using Bishop.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests;

public sealed class DbFixture : IDisposable
{
    public SqliteConnection Connection { get; }
    public BishopDbContext Db { get; }

    public DbFixture()
    {
        // Named shared-cache in-memory database: unique per fixture, persists for
        // the lifetime of this connection, isolated from other DbFixture instances.
        var dbName = Guid.NewGuid().ToString("N");
        Connection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        Connection.Open();
        Db = new BishopDbContext(new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(Connection)
            .Options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        Connection.Dispose();
    }
}

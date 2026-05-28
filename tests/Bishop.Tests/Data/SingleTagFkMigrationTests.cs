using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace Bishop.Tests.Data;

public sealed class SingleTagFkMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SingleTagFkMigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        SetupOldSchema();
    }

    public void Dispose() => _connection.Dispose();

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void SetupOldSchema()
    {
        Execute("CREATE TABLE Tags (Id TEXT PRIMARY KEY, WorkspaceId TEXT, Name TEXT, Colour TEXT, CreatedAt TEXT, UpdatedAt TEXT)");
        Execute("CREATE TABLE Cards (Id TEXT PRIMARY KEY, LaneId TEXT, TagId TEXT, Title TEXT, Description TEXT, Number INTEGER, Position INTEGER, IsClosed INTEGER DEFAULT 0, CreatedAt TEXT, UpdatedAt TEXT)");
        Execute("CREATE TABLE CardTags (CardId TEXT, TagId TEXT, PRIMARY KEY (CardId, TagId))");
    }

    private Guid Insert(string table, string columns, string values, params object[] args)
    {
        using var cmd = _connection.CreateCommand();
        for (var i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i]);
        cmd.CommandText = $"INSERT INTO {table} ({columns}) VALUES ({values})";
        cmd.ExecuteNonQuery();
        return Guid.Empty;
    }

    private void RunBackfillSql()
    {
        Execute("UPDATE Cards SET TagId = (SELECT TagId FROM CardTags WHERE CardId = Cards.Id ORDER BY rowid LIMIT 1)");
    }

    private string? ReadTagId(Guid cardId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT TagId FROM Cards WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", cardId.ToString());
        return cmd.ExecuteScalar() as string;
    }

    [Fact]
    public void Backfill_MultiTaggedCard_CollapsesToFirstByRowid()
    {
        // Arrange — card with two tags; first inserted should win
        var cardId = Guid.NewGuid();
        var tag1 = Guid.NewGuid();
        var tag2 = Guid.NewGuid();

        Execute($"INSERT INTO Cards (Id, LaneId, TagId, Title, Description, Number, Position, CreatedAt, UpdatedAt) VALUES ('{cardId}', '{Guid.NewGuid()}', NULL, 'C', '', 1, 1, '2026-01-01', '2026-01-01')");
        Execute($"INSERT INTO Tags (Id, WorkspaceId, Name, Colour, CreatedAt, UpdatedAt) VALUES ('{tag1}', '{Guid.NewGuid()}', 'bug', '#ff0000', '2026-01-01', '2026-01-01')");
        Execute($"INSERT INTO Tags (Id, WorkspaceId, Name, Colour, CreatedAt, UpdatedAt) VALUES ('{tag2}', '{Guid.NewGuid()}', 'feature', '#00ff00', '2026-01-01', '2026-01-01')");
        Execute($"INSERT INTO CardTags (CardId, TagId) VALUES ('{cardId}', '{tag1}')");
        Execute($"INSERT INTO CardTags (CardId, TagId) VALUES ('{cardId}', '{tag2}')");

        // Act
        RunBackfillSql();

        // Assert — tag1 was inserted first (lower rowid), so it wins
        ReadTagId(cardId).Should().Be(tag1.ToString(), because: "first CardTag by rowid is preserved");
    }

    [Fact]
    public void Backfill_SingleTaggedCard_KeepsItsTag()
    {
        var cardId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        Execute($"INSERT INTO Cards (Id, LaneId, TagId, Title, Description, Number, Position, CreatedAt, UpdatedAt) VALUES ('{cardId}', '{Guid.NewGuid()}', NULL, 'C', '', 1, 1, '2026-01-01', '2026-01-01')");
        Execute($"INSERT INTO Tags (Id, WorkspaceId, Name, Colour, CreatedAt, UpdatedAt) VALUES ('{tagId}', '{Guid.NewGuid()}', 'bug', '#ff0000', '2026-01-01', '2026-01-01')");
        Execute($"INSERT INTO CardTags (CardId, TagId) VALUES ('{cardId}', '{tagId}')");

        RunBackfillSql();

        ReadTagId(cardId).Should().Be(tagId.ToString());
    }

    [Fact]
    public void Backfill_UntaggedCard_RemainsNull()
    {
        var cardId = Guid.NewGuid();
        Execute($"INSERT INTO Cards (Id, LaneId, TagId, Title, Description, Number, Position, CreatedAt, UpdatedAt) VALUES ('{cardId}', '{Guid.NewGuid()}', NULL, 'C', '', 1, 1, '2026-01-01', '2026-01-01')");

        RunBackfillSql();

        ReadTagId(cardId).Should().BeNull();
    }
}

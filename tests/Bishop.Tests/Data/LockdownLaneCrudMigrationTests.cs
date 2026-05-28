using System.Reflection;
using Bishop.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Bishop.Tests.Data;

public sealed class LockdownLaneCrudMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public LockdownLaneCrudMigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        SetupSchema();
    }

    public void Dispose() => _connection.Dispose();

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void SetupSchema()
    {
        Execute("""
            CREATE TABLE "Workspaces" (
                "Id" TEXT PRIMARY KEY,
                "Name" TEXT, "Path" TEXT, "Position" INTEGER,
                "CreatedAt" TEXT, "UpdatedAt" TEXT
            )
            """);
        Execute("""
            CREATE TABLE "Lanes" (
                "Id" TEXT PRIMARY KEY,
                "WorkspaceId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Position" INTEGER NOT NULL,
                "IsSystem" INTEGER NOT NULL DEFAULT 0
            )
            """);
        Execute("""
            CREATE TABLE "Cards" (
                "Id" TEXT PRIMARY KEY,
                "LaneId" TEXT NOT NULL,
                "Title" TEXT, "Description" TEXT,
                "Number" INTEGER, "Position" INTEGER NOT NULL,
                "IsClosed" INTEGER DEFAULT 0,
                "CreatedAt" TEXT, "UpdatedAt" TEXT
            )
            """);
    }

    private void RunMigration()
    {
        // Drive the real Up() so the test never drifts from the shipped SQL.
        var migration = new LockdownLaneCrud();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        typeof(Migration)
            .GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(MigrationBuilder)])!
            .Invoke(migration, [builder]);
        foreach (var op in builder.Operations.OfType<SqlOperation>())
            Execute(op.Sql);
    }

    private void InsertWorkspace(Guid id, string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "Workspaces" ("Id", "Name", "Path", "Position", "CreatedAt", "UpdatedAt")
            VALUES (@id, @name, @path, 1, '2026-01-01', '2026-01-01')
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@path", $@"C:\{name}");
        cmd.ExecuteNonQuery();
    }

    private Guid InsertLane(Guid workspaceId, string name, int position, bool isSystem)
    {
        var id = Guid.NewGuid();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "Lanes" ("Id", "WorkspaceId", "Name", "Position", "IsSystem")
            VALUES (@id, @ws, @name, @pos, @sys)
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@ws", workspaceId.ToString());
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@pos", position);
        cmd.Parameters.AddWithValue("@sys", isSystem ? 1 : 0);
        cmd.ExecuteNonQuery();
        return id;
    }

    private Guid InsertCard(Guid laneId, string title, int position)
    {
        var id = Guid.NewGuid();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "Cards" ("Id", "LaneId", "Title", "Description", "Number", "Position", "CreatedAt", "UpdatedAt")
            VALUES (@id, @lane, @title, '', 0, @pos, '2026-01-01', '2026-01-01')
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@lane", laneId.ToString());
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@pos", position);
        cmd.ExecuteNonQuery();
        return id;
    }

    private List<(string Name, int Position, bool IsSystem)> ReadLanes(Guid workspaceId)
    {
        var rows = new List<(string, int, bool)>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT "Name", "Position", "IsSystem" FROM "Lanes"
            WHERE "WorkspaceId" = @ws ORDER BY "Position"
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId.ToString());
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            rows.Add((rdr.GetString(0), rdr.GetInt32(1), rdr.GetInt64(2) == 1));
        return rows;
    }

    private List<(string Title, string LaneName, int Position)> ReadCards(Guid workspaceId)
    {
        var rows = new List<(string, string, int)>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT c."Title", l."Name", c."Position"
            FROM "Cards" c
            INNER JOIN "Lanes" l ON l."Id" = c."LaneId"
            WHERE l."WorkspaceId" = @ws
            ORDER BY l."Position", c."Position"
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId.ToString());
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            rows.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetInt32(2)));
        return rows;
    }

    [Fact]
    public void Up_PromotesPreExistingBacklogToSystem()
    {
        var wsId = Guid.NewGuid();
        InsertWorkspace(wsId, "ws");
        InsertLane(wsId, "Backlog", 1, isSystem: false); // user-created Backlog
        InsertLane(wsId, "To Do", 2, isSystem: true);
        InsertLane(wsId, "Doing", 3, isSystem: true);
        InsertLane(wsId, "Done", 4, isSystem: true);

        RunMigration();

        var lanes = ReadLanes(wsId);
        lanes.Should().HaveCount(4);
        lanes.Should().AllSatisfy(l => l.IsSystem.Should().BeTrue());
        lanes.First(l => l.Name == "Backlog").IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Up_CreatesBacklog_AndShiftsPositions_WhenMissing()
    {
        var wsId = Guid.NewGuid();
        InsertWorkspace(wsId, "ws");
        // No Backlog; the original AddLaneIsSystem state.
        InsertLane(wsId, "To Do", 1, isSystem: true);
        InsertLane(wsId, "Doing", 2, isSystem: true);
        InsertLane(wsId, "Done", 3, isSystem: true);

        RunMigration();

        var lanes = ReadLanes(wsId);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
        lanes.Should().AllSatisfy(l => l.IsSystem.Should().BeTrue());
    }

    [Fact]
    public void Up_MovesCardsFromNonSystemLane_ToBacklog_AppendedAfterExisting()
    {
        var wsId = Guid.NewGuid();
        InsertWorkspace(wsId, "ws");
        var backlog = InsertLane(wsId, "Backlog", 1, isSystem: true);
        InsertLane(wsId, "To Do", 2, isSystem: true);
        InsertLane(wsId, "Doing", 3, isSystem: true);
        InsertLane(wsId, "Done", 4, isSystem: true);
        var review = InsertLane(wsId, "Review", 5, isSystem: false);

        InsertCard(backlog, "B1", 1);
        InsertCard(backlog, "B2", 2);
        InsertCard(review, "R1", 1);
        InsertCard(review, "R2", 2);

        RunMigration();

        ReadLanes(wsId).Select(l => l.Name)
            .Should().Equal("Backlog", "To Do", "Doing", "Done");

        var backlogCards = ReadCards(wsId)
            .Where(c => c.LaneName == "Backlog")
            .OrderBy(c => c.Position)
            .ToList();
        backlogCards.Select(c => c.Title).Should().Equal("B1", "B2", "R1", "R2");
        backlogCards.Select(c => c.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Up_MergesCardsFromMultipleNonSystemLanes_PreservingLaneAndCardOrder()
    {
        var wsId = Guid.NewGuid();
        InsertWorkspace(wsId, "ws");
        InsertLane(wsId, "Backlog", 1, isSystem: true);
        InsertLane(wsId, "To Do", 2, isSystem: true);
        InsertLane(wsId, "Doing", 3, isSystem: true);
        InsertLane(wsId, "Done", 4, isSystem: true);
        var review = InsertLane(wsId, "Review", 5, isSystem: false);
        var archive = InsertLane(wsId, "Archive", 6, isSystem: false);

        InsertCard(review, "R1", 1);
        InsertCard(review, "R2", 2);
        InsertCard(archive, "A1", 1);

        RunMigration();

        var backlogCards = ReadCards(wsId)
            .Where(c => c.LaneName == "Backlog")
            .OrderBy(c => c.Position)
            .ToList();
        backlogCards.Select(c => c.Title).Should().Equal("R1", "R2", "A1");
        backlogCards.Select(c => c.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Up_DeletesNonSystemLanes()
    {
        var wsId = Guid.NewGuid();
        InsertWorkspace(wsId, "ws");
        InsertLane(wsId, "Backlog", 1, isSystem: true);
        InsertLane(wsId, "To Do", 2, isSystem: true);
        InsertLane(wsId, "Doing", 3, isSystem: true);
        InsertLane(wsId, "Done", 4, isSystem: true);
        InsertLane(wsId, "Review", 5, isSystem: false);
        InsertLane(wsId, "Archive", 6, isSystem: false);

        RunMigration();

        var lanes = ReadLanes(wsId);
        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Up_IsScopedPerWorkspace()
    {
        var ws1 = Guid.NewGuid();
        var ws2 = Guid.NewGuid();
        InsertWorkspace(ws1, "ws1");
        InsertWorkspace(ws2, "ws2");

        var backlog1 = InsertLane(ws1, "Backlog", 1, isSystem: true);
        InsertLane(ws1, "To Do", 2, isSystem: true);
        var review1 = InsertLane(ws1, "Review", 3, isSystem: false);
        InsertCard(backlog1, "B", 1);
        InsertCard(review1, "R", 1);

        var backlog2 = InsertLane(ws2, "Backlog", 1, isSystem: true);
        InsertCard(backlog2, "X", 1);

        RunMigration();

        var ws1Cards = ReadCards(ws1)
            .Where(c => c.LaneName == "Backlog")
            .OrderBy(c => c.Position)
            .Select(c => c.Title)
            .ToList();
        ws1Cards.Should().Equal("B", "R");

        var ws2Cards = ReadCards(ws2)
            .Where(c => c.LaneName == "Backlog")
            .OrderBy(c => c.Position)
            .Select(c => c.Title)
            .ToList();
        ws2Cards.Should().Equal("X");
    }
}

using Bishop.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Data;

public sealed class BishopDbContextAuditTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAtAndUpdatedAt_OnAddedWorkspace()
    {
        var before = DateTimeOffset.UtcNow;
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W", Path = "/w" };
        _fixture.Db.Workspaces.Add(workspace);
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        workspace.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        workspace.UpdatedAt.Should().Be(workspace.CreatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_SetsUpdatedAt_OnModifiedWorkspace()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W", Path = "/w" };
        _fixture.Db.Workspaces.Add(workspace);
        await _fixture.Db.SaveChangesAsync();
        var createdAt = workspace.CreatedAt;

        workspace.Name = "W2";
        var before = DateTimeOffset.UtcNow;
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        workspace.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        workspace.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAtAndUpdatedAt_OnAddedCard()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W", Path = "/w" };
        _fixture.Db.Workspaces.Add(workspace);
        await _fixture.Db.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, LaneName = SystemLaneNames.ToDo, Title = "C", Number = 1 };
        _fixture.Db.Cards.Add(card);
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        card.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        card.UpdatedAt.Should().Be(card.CreatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_DoesNotAudit_NonIAuditableEntity()
    {
        var setting = new AppSetting { Key = "theme", Value = "dark" };
        _fixture.Db.AppSettings.Add(setting);

        _fixture.Db.ChangeTracker.Entries<IAuditable>().Should().BeEmpty();

        await _fixture.Db.SaveChangesAsync();

        var loaded = await _fixture.Db.AppSettings.FindAsync("theme");
        loaded.Should().NotBeNull();
        loaded!.Value.Should().Be("dark");
    }

    [Theory]
    [InlineData("Workspaces")]
    [InlineData("Cards")]
    public void OnModelCreating_SetsNocaseCollation_OnGuidColumns(string tableName)
    {
        using var cmd = _fixture.Connection.CreateCommand();
        cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}'";
        var ddl = (string)cmd.ExecuteScalar()!;

        ddl.Should().Contain("COLLATE NOCASE", because: $"all Guid columns in {tableName} must use NOCASE collation");
    }

    [Fact]
    public async Task AppSettings_CanRoundTrip()
    {
        var setting = new AppSetting { Key = "mode", Value = "dark" };
        _fixture.Db.AppSettings.Add(setting);
        await _fixture.Db.SaveChangesAsync();

        _fixture.Db.ChangeTracker.Clear();
        var loaded = await _fixture.Db.AppSettings.FindAsync("mode");
        loaded.Should().NotBeNull();
        loaded!.Value.Should().Be("dark");
    }

    [Fact]
    public async Task Card_TagName_CanRoundTrip()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W2", Path = "/w2" };
        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            LaneName = SystemLaneNames.ToDo,
            Title = "C",
            Number = 1,
            TagName = TagNames.Feature,
        };
        _fixture.Db.Workspaces.Add(workspace);
        _fixture.Db.Cards.Add(card);
        await _fixture.Db.SaveChangesAsync();

        _fixture.Db.ChangeTracker.Clear();
        var loaded = await _fixture.Db.Cards
            .FirstAsync(c => c.Id == card.Id);
        loaded.TagName.Should().Be(TagNames.Feature);
    }
}

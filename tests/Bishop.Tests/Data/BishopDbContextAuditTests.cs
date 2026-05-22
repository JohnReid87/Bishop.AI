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
        var lane = new Lane { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "To Do", Position = 1 };
        _fixture.Db.Workspaces.Add(workspace);
        _fixture.Db.Lanes.Add(lane);
        await _fixture.Db.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var card = new Card { Id = Guid.NewGuid(), LaneId = lane.Id, Title = "C", Number = 1 };
        _fixture.Db.Cards.Add(card);
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        card.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        card.UpdatedAt.Should().Be(card.CreatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAtAndUpdatedAt_OnAddedTag()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W", Path = "/w" };
        _fixture.Db.Workspaces.Add(workspace);
        await _fixture.Db.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "feat" };
        _fixture.Db.Tags.Add(tag);
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        tag.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        tag.UpdatedAt.Should().Be(tag.CreatedAt);
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
    [InlineData("Lanes")]
    [InlineData("Cards")]
    [InlineData("Tags")]
    [InlineData("CardTags")]
    public void OnModelCreating_SetsNocaseCollation_OnGuidColumns(string tableName)
    {
        // Verify via the SQLite schema that every table with Guid columns carries COLLATE NOCASE.
        // EnsureCreated() already ran in DbFixture, so sqlite_master is populated.
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
    public async Task CardTags_CanRoundTrip()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "W", Path = "/w" };
        var lane = new Lane { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "To Do", Position = 1 };
        var card = new Card { Id = Guid.NewGuid(), LaneId = lane.Id, Title = "C", Number = 1 };
        var tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "feat" };
        _fixture.Db.Workspaces.Add(workspace);
        _fixture.Db.Lanes.Add(lane);
        _fixture.Db.Cards.Add(card);
        _fixture.Db.Tags.Add(tag);
        await _fixture.Db.SaveChangesAsync();

        var cardTag = new CardTag { CardId = card.Id, TagId = tag.Id };
        _fixture.Db.CardTags.Add(cardTag);
        await _fixture.Db.SaveChangesAsync();

        _fixture.Db.ChangeTracker.Clear();
        var loaded = await _fixture.Db.CardTags.FindAsync(card.Id, tag.Id);
        loaded.Should().NotBeNull();
        loaded!.CardId.Should().Be(card.Id);
        loaded.TagId.Should().Be(tag.Id);
    }
}

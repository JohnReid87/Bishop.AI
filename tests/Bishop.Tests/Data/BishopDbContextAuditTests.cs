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

        workspace.Name = "W2";
        var before = DateTimeOffset.UtcNow;
        await _fixture.Db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        workspace.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
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
}

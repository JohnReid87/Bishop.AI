using Bishop.App;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App;

public sealed class WorkspaceSkillRunCleanupTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fixture;

    public WorkspaceSkillRunCleanupTests(DbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StartAsync_CollapsesNonAllowlistedPerProjectRows()
    {
        var workspaceId = _fixture.SeedWorkspace();
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        _fixture.Db.WorkspaceSkillRuns.AddRange(
            new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-coverage", ProjectName = "Bishop.App", GitSha = "sha-a", RecordedAt = older, FindingsCount = 1 },
            new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-coverage", ProjectName = "Bishop.UI", GitSha = "sha-b", RecordedAt = newer, FindingsCount = 2 });
        await _fixture.Db.SaveChangesAsync();

        var sut = new WorkspaceSkillRunCleanup(_fixture.Factory);
        await sut.StartAsync(default);

        var runs = await _fixture.Db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId && r.SkillName == "bish-coverage")
            .ToListAsync();
        runs.Should().HaveCount(1);
        runs[0].ProjectName.Should().BeNull();
        runs[0].GitSha.Should().Be("sha-b", "the most-recent run is retained");
    }

    [Fact]
    public async Task StartAsync_LeavesAllowlistedPerProjectRowsAlone()
    {
        var workspaceId = _fixture.SeedWorkspace();

        _fixture.Db.WorkspaceSkillRuns.AddRange(
            new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", ProjectName = "Bishop.App.Tests", GitSha = "sha-a", RecordedAt = DateTimeOffset.UtcNow, FindingsCount = 0 },
            new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", ProjectName = "Bishop.Core.Tests", GitSha = "sha-b", RecordedAt = DateTimeOffset.UtcNow, FindingsCount = 0 });
        await _fixture.Db.SaveChangesAsync();

        var sut = new WorkspaceSkillRunCleanup(_fixture.Factory);
        await sut.StartAsync(default);

        var runs = await _fixture.Db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId && r.SkillName == "bish-tests")
            .ToListAsync();
        runs.Should().HaveCount(2);
        runs.Select(r => r.ProjectName).Should().BeEquivalentTo(new[] { "Bishop.App.Tests", "Bishop.Core.Tests" });
    }

    [Fact]
    public async Task StartAsync_DropsPerProjectRowsWhenGenericRowAlreadyExists()
    {
        var workspaceId = _fixture.SeedWorkspace();
        var genericId = Guid.NewGuid();

        _fixture.Db.WorkspaceSkillRuns.AddRange(
            new WorkspaceSkillRun { Id = genericId, WorkspaceId = workspaceId, SkillName = "bish-arch", ProjectName = null, GitSha = "sha-generic", RecordedAt = DateTimeOffset.UtcNow, FindingsCount = 3 },
            new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-arch", ProjectName = "Bishop.App", GitSha = "sha-a", RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-5), FindingsCount = 1 });
        await _fixture.Db.SaveChangesAsync();

        var sut = new WorkspaceSkillRunCleanup(_fixture.Factory);
        await sut.StartAsync(default);

        var runs = await _fixture.Db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId && r.SkillName == "bish-arch")
            .ToListAsync();
        runs.Should().HaveCount(1);
        runs[0].Id.Should().Be(genericId);
    }
}

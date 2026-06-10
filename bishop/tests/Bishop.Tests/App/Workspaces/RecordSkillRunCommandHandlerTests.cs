using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.App.Workspaces.RecordSkillRun;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Workspaces;

public sealed class RecordSkillRunCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public RecordSkillRunCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U();
        return await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    [Fact]
    public async Task Handle_InsertsRow_OnFirstRecord()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordSkillRunCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordSkillRunCommand(ws.Id, "bish-arch", "abc1234"), default);

        var run = await _db.WorkspaceSkillRuns.AsNoTracking()
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch");
        run.GitSha.Should().Be("abc1234");
        run.RecordedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_UpdatesExistingRow_OnSubsequentRecord()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordSkillRunCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordSkillRunCommand(ws.Id, "bish-arch", "aaa1111"), default);
        await sut.Handle(new RecordSkillRunCommand(ws.Id, "bish-arch", "bbb2222"), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch")
            .ToListAsync();
        runs.Should().HaveCount(1);
        runs[0].GitSha.Should().Be("bbb2222");
    }

    [Fact]
    public async Task Handle_StoresOneRowPerSkill_ForSameWorkspace()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordSkillRunCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordSkillRunCommand(ws.Id, "bish-arch", "sha1"), default);
        await sut.Handle(new RecordSkillRunCommand(ws.Id, "bish-coverage", "sha2"), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id)
            .ToListAsync();
        runs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetWorkspaceSkillRunsQuery_ReturnsRunsForWorkspace_OrderedBySkillName()
    {
        var ws = await CreateWorkspaceAsync();
        var recordSut = new RecordSkillRunCommandHandler(_factory, TimeProvider.System);
        await recordSut.Handle(new RecordSkillRunCommand(ws.Id, "bish-security", "s1"), default);
        await recordSut.Handle(new RecordSkillRunCommand(ws.Id, "bish-arch", "s2"), default);

        var querySut = new GetWorkspaceSkillRunsQueryHandler(_factory);
        var result = await querySut.Handle(new GetWorkspaceSkillRunsQuery(ws.Id), default);

        result.Should().HaveCount(2);
        result[0].SkillName.Should().Be("bish-arch");
        result[1].SkillName.Should().Be("bish-security");
    }
}

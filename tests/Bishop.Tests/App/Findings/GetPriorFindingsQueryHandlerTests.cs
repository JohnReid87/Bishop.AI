using Bishop.App.Findings.GetPriorFindings;
using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Findings;

public sealed class GetPriorFindingsQueryHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _tempRoot;

    public GetPriorFindingsQueryHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "bishop-prior-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = $"ws-{Guid.NewGuid():N}"[..20];
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, Path.Combine(_tempRoot, name)), default);
    }

    [Fact]
    public async Task Handle_ReturnsAllFindingsForSkill_AcrossProjectsAndStatuses()
    {
        var ws = await CreateWorkspaceAsync();
        var record = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            {
              "projectName": "Bishop.App",
              "findings": [
                { "title": "T1", "body": "b", "outcome": "dismissed",
                  "file": "src/F.cs", "rule": "R", "symbol": "S" }
              ]
            }
            """;
        await record.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"), default);

        // Mark as dismissed with a rebuttal.
        var f = await _db.Findings.SingleAsync(x => x.Run.WorkspaceId == ws.Id);
        f.Status = "dismissed";
        f.RebuttalText = "Reflected via DI.";
        f.LinkedCardId = 7;
        await _db.SaveChangesAsync();

        var sut = new GetPriorFindingsQueryHandler(_factory);
        var result = await sut.Handle(new GetPriorFindingsQuery(ws.Id, "bish-arch"), default);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be("dismissed");
        result[0].RebuttalText.Should().Be("Reflected via DI.");
        result[0].LinkedCardNumber.Should().Be(7);
        result[0].File.Should().Be("src/F.cs");
        result[0].Rule.Should().Be("R");
        result[0].Symbol.Should().Be("S");
        result[0].ProjectName.Should().Be("Bishop.App");
    }

    [Fact]
    public async Task Handle_ScopesBySkillAndWorkspace()
    {
        var wsA = await CreateWorkspaceAsync();
        var wsB = await CreateWorkspaceAsync();
        var record = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
        const string json = """{ "findings": [ { "title": "A", "body": "b", "outcome": "dismissed" } ] }""";

        await record.Handle(new RecordFindingsCommand(wsA.Id, wsA.Path, "bish-arch", json, "sha"), default);
        await record.Handle(new RecordFindingsCommand(wsB.Id, wsB.Path, "bish-arch", json, "sha"), default);
        await record.Handle(new RecordFindingsCommand(wsA.Id, wsA.Path, "bish-security", json, "sha"), default);

        var sut = new GetPriorFindingsQueryHandler(_factory);

        var archA = await sut.Handle(new GetPriorFindingsQuery(wsA.Id, "bish-arch"), default);
        archA.Should().HaveCount(1);

        var secA = await sut.Handle(new GetPriorFindingsQuery(wsA.Id, "bish-security"), default);
        secA.Should().HaveCount(1);
    }
}

using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Findings;

public sealed class GetFindingsBySkillAndProjectQueryHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _tempRoot;

    public GetFindingsBySkillAndProjectQueryHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "bishop-getfindings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = "ws-" + Guid.NewGuid().ToString("N")[..8];
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, Path.Combine(_tempRoot, name)), default);
    }

    private async Task RecordAsync(Workspace ws, string skill, string json, string sha = "sha")
        => await new RecordFindingsCommandHandler(_factory, TimeProvider.System)
            .Handle(new RecordFindingsCommand(ws.Id, ws.Path, skill, json, sha), default);

    [Fact]
    public async Task ReturnsFindingsForMatchingSkillAndNullProject()
    {
        var ws = await CreateWorkspaceAsync();
        await RecordAsync(ws, "bish-arch", """
            { "findings": [
                { "title": "A", "body": "ba", "severity": "high", "outcome": "dismissed", "file": "src/A.cs" },
                { "title": "B", "body": "bb", "outcome": "parked" }
            ] }
            """);

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);
        var result = await sut.Handle(new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-arch", null), default);

        result.Should().HaveCount(2);
        result.Select(f => f.Title).Should().BeEquivalentTo(["A", "B"]);
        result.Single(f => f.Title == "A").File.Should().Be("src/A.cs");
        result.Single(f => f.Title == "A").Severity.Should().Be("high");
    }

    [Fact]
    public async Task FiltersByProjectName()
    {
        var ws = await CreateWorkspaceAsync();
        await RecordAsync(ws, "bish-tests", """
            { "projectName": "Bishop.App", "findings": [ { "title": "AppOnly", "body": "x", "outcome": "dismissed" } ] }
            """);
        await RecordAsync(ws, "bish-tests", """
            { "projectName": "Bishop.UI", "findings": [ { "title": "UiOnly", "body": "y", "outcome": "dismissed" } ] }
            """);

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);

        var appResult = await sut.Handle(
            new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-tests", "Bishop.App"), default);
        var uiResult = await sut.Handle(
            new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-tests", "Bishop.UI"), default);

        appResult.Should().ContainSingle(f => f.Title == "AppOnly");
        uiResult.Should().ContainSingle(f => f.Title == "UiOnly");
    }

    [Fact]
    public async Task ReturnsEmptyWhenNoMatch()
    {
        var ws = await CreateWorkspaceAsync();

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);
        var result = await sut.Handle(new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-arch", null), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkedCardIsClosed_IsNullForUnlinkedFindings()
    {
        var ws = await CreateWorkspaceAsync();
        await RecordAsync(ws, "bish-arch", """{ "findings": [ { "title": "Unlinked", "body": "x", "outcome": "parked" } ] }""");

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);
        var result = await sut.Handle(new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-arch", null), default);

        result.Should().ContainSingle().Which.LinkedCardIsClosed.Should().BeNull();
    }

    [Fact]
    public async Task LinkedCardIsClosed_ReflectsCardStateForLinkedFinding()
    {
        var ws = await CreateWorkspaceAsync();
        await RecordAsync(ws, "bish-arch", """
            { "findings": [
                { "title": "OpenLink", "body": "x", "outcome": "parked" },
                { "title": "ClosedLink", "body": "y", "outcome": "parked" },
                { "title": "DanglingLink", "body": "z", "outcome": "parked" }
            ] }
            """);

        var openCard = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws.Id,
            Number = 1,
            Title = "open",
            LaneName = "To Do",
            IsClosed = false,
        };
        var doneCard = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws.Id,
            Number = 2,
            Title = "done",
            LaneName = "Done",
            IsClosed = true,
        };
        _db.Cards.AddRange(openCard, doneCard);

        var findings = await _db.Findings.Where(f => f.Run.WorkspaceId == ws.Id).ToListAsync();
        findings.Single(f => f.Title == "OpenLink").LinkedCardId = 1;
        findings.Single(f => f.Title == "ClosedLink").LinkedCardId = 2;
        findings.Single(f => f.Title == "DanglingLink").LinkedCardId = 999;
        await _db.SaveChangesAsync();

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);
        var result = await sut.Handle(new GetFindingsBySkillAndProjectQuery(ws.Id, "bish-arch", null), default);

        result.Single(f => f.Title == "OpenLink").LinkedCardIsClosed.Should().BeFalse();
        result.Single(f => f.Title == "ClosedLink").LinkedCardIsClosed.Should().BeTrue();
        result.Single(f => f.Title == "DanglingLink").LinkedCardIsClosed.Should().BeNull();
    }

    [Fact]
    public async Task DoesNotIncludeFindingsFromOtherWorkspaces()
    {
        var ws1 = await CreateWorkspaceAsync();
        var ws2 = await CreateWorkspaceAsync();
        await RecordAsync(ws1, "bish-arch", """{ "findings": [ { "title": "WS1", "body": "x", "outcome": "dismissed" } ] }""");
        await RecordAsync(ws2, "bish-arch", """{ "findings": [ { "title": "WS2", "body": "x", "outcome": "dismissed" } ] }""");

        var sut = new GetFindingsBySkillAndProjectQueryHandler(_factory);
        var result = await sut.Handle(new GetFindingsBySkillAndProjectQuery(ws1.Id, "bish-arch", null), default);

        result.Should().ContainSingle().Which.Title.Should().Be("WS1");
    }
}

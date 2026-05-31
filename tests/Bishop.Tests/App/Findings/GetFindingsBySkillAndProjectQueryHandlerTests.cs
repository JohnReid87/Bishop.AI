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

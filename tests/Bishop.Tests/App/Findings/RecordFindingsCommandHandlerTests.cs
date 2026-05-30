using Bishop.App.Findings;
using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Findings;

public sealed class RecordFindingsCommandHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _tempRoot;

    public RecordFindingsCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "bishop-findings-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U();
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, Path.Combine(_tempRoot, name)), default);
    }

    private const string ValidJson = """
        {
          "findings": [
            {
              "title": "Public type with no references",
              "body": "UnusedHelper is not called anywhere.",
              "severity": "low",
              "location": "src/Foo.cs:42",
              "outcome": "carded:#123"
            },
            {
              "title": "DTO defined twice",
              "body": "Two UserDto records exist.",
              "outcome": "dismissed"
            }
          ]
        }
        """;

    [Fact]
    public async Task Handle_HappyPath_WritesJsonAndHtmlAndRecordsRun()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-dead-code", ValidJson, "abc1234"),
            default);

        result.FindingCount.Should().Be(2);
        File.Exists(result.JsonPath).Should().BeTrue();
        File.Exists(result.HtmlPath).Should().BeTrue();
        result.JsonPath.Should().Be(Path.Combine(ws.Path, ".bishop", "findings", "bish-dead-code.json"));
        result.HtmlPath.Should().Be(Path.Combine(ws.Path, ".bishop", "findings", "bish-dead-code.html"));

        var run = await _db.WorkspaceSkillRuns.AsNoTracking()
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-dead-code");
        run.GitSha.Should().Be("abc1234");
        run.RecordedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        run.FindingsCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_HtmlContainsFindingTitles()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", ValidJson, "sha1"),
            default);

        var html = await File.ReadAllTextAsync(result.HtmlPath);
        html.Should().Contain("Public type with no references");
        html.Should().Contain("DTO defined twice");
        html.Should().Contain("src/Foo.cs:42");
        html.Should().Contain("#123");
        html.Should().Contain("dismissed");
    }

    [Fact]
    public async Task Handle_OverwritesExistingFiles_OnSecondRun()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", ValidJson, "sha1"), default);

        const string smallerJson = """
            { "findings": [ { "title": "Only one", "body": "Just this.", "outcome": "parked" } ] }
            """;
        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", smallerJson, "sha2"),
            default);

        result.FindingCount.Should().Be(1);
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        html.Should().Contain("Only one");
        html.Should().NotContain("Public type with no references");

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch")
            .ToListAsync();
        runs.Should().HaveCount(1);
        runs[0].GitSha.Should().Be("sha2");
        runs[0].FindingsCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MalformedJson_Throws()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", "{not json", "sha"),
            default);

        await act.Should().ThrowAsync<FindingsValidationException>()
            .WithMessage("*malformed*");
    }

    [Fact]
    public async Task Handle_MissingFindingsArray_Throws()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", "{}", "sha"),
            default);

        await act.Should().ThrowAsync<FindingsValidationException>()
            .WithMessage("*'findings' array*");
    }

    [Fact]
    public async Task Handle_MissingTitle_Throws()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);
        const string json = """{ "findings": [ { "body": "x", "outcome": "dismissed" } ] }""";

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"),
            default);

        await act.Should().ThrowAsync<FindingsValidationException>()
            .WithMessage("*title*required*");
    }

    [Fact]
    public async Task Handle_InvalidOutcome_Throws()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);
        const string json = """{ "findings": [ { "title": "x", "body": "y", "outcome": "skipped" } ] }""";

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"),
            default);

        await act.Should().ThrowAsync<FindingsValidationException>()
            .WithMessage("*outcome*");
    }

    [Fact]
    public async Task Handle_CardedOutcomeMustBeNumeric()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);
        const string json = """{ "findings": [ { "title": "x", "body": "y", "outcome": "carded:#abc" } ] }""";

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"),
            default);

        await act.Should().ThrowAsync<FindingsValidationException>();
    }

    [Theory]
    [InlineData(@"..\..\evil")]
    [InlineData("../../evil")]
    [InlineData(@"sub\dir")]
    [InlineData("sub/dir")]
    public async Task Handle_SkillNameWithPathTraversal_Throws(string skillName)
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, skillName, ValidJson, "sha"),
            default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*path separators*");

        var findingsDir = Path.Combine(ws.Path, ".bishop", "findings");
        Directory.Exists(findingsDir).Should().BeFalse("no file write should occur before the guard throws");
    }

    [Fact]
    public async Task Handle_EmptyFindingsArray_Succeeds()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory);
        const string json = """{ "findings": [] }""";

        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"),
            default);

        result.FindingCount.Should().Be(0);
        File.Exists(result.JsonPath).Should().BeTrue();
        File.Exists(result.HtmlPath).Should().BeTrue();
    }
}

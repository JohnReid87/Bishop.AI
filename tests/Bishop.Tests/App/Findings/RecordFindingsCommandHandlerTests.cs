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
    public async Task Handle_HappyPath_PersistsFindings()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-dead-code", ValidJson, "abc1234"),
            default);

        result.FindingCount.Should().Be(2);

        var run = await _db.WorkspaceSkillRuns.AsNoTracking()
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-dead-code");
        run.GitSha.Should().Be("abc1234");
        run.RecordedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        run.FindingsCount.Should().Be(2);
        run.ProjectName.Should().BeNull();

        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.WorkspaceSkillRunId == run.Id)
            .OrderBy(f => f.Title)
            .ToListAsync();
        findings.Should().HaveCount(2);
        findings.All(f => !string.IsNullOrEmpty(f.IdentityHash)).Should().BeTrue();

        // ValidJson declares the first finding as carded:#123 and the second as dismissed,
        // so the recorder must persist those outcomes at insert time.
        var carded = findings.Single(f => f.Title == "Public type with no references");
        carded.Status.Should().Be("carded");
        carded.LinkedCardId.Should().Be(123);

        var dismissed = findings.Single(f => f.Title == "DTO defined twice");
        dismissed.Status.Should().Be("dismissed");
        dismissed.LinkedCardId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NewParkedFinding_StoredAsPending()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
        const string json = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"), default);

        var stored = await _db.Findings.AsNoTracking()
            .Include(f => f.Run)
            .SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        stored.Status.Should().Be("pending");
        stored.LinkedCardId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SkillUpgradesPendingToCarded_OnRerun()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string parkedJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", parkedJson, "sha1"), default);

        const string cardedJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "carded:#77",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", cardedJson, "sha2"), default);

        var stored = await _db.Findings.AsNoTracking()
            .Include(f => f.Run)
            .SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        stored.Status.Should().Be("carded");
        stored.LinkedCardId.Should().Be(77);
    }

    [Fact]
    public async Task Handle_SkillCardedOutcome_DoesNotOverwriteUserDismissal()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string parkedJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", parkedJson, "sha1"), default);

        var f = await _db.Findings
            .Include(x => x.Run)
            .SingleAsync(x => x.Run.WorkspaceId == ws.Id);
        f.Status = "dismissed";
        f.RebuttalText = "not real";
        await _db.SaveChangesAsync();

        const string cardedJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "carded:#9",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", cardedJson, "sha2"), default);

        var stored = await _db.Findings.AsNoTracking()
            .Include(f => f.Run)
            .SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        stored.Status.Should().Be("dismissed", "a user dismissal outranks a later skill-emitted card link");
        stored.LinkedCardId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SkillCardedOutcome_DoesNotChangeExistingLink()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string firstJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "carded:#10",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", firstJson, "sha1"), default);

        const string secondJson = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "carded:#99",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", secondJson, "sha2"), default);

        var stored = await _db.Findings.AsNoTracking()
            .Include(f => f.Run)
            .SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        stored.Status.Should().Be("carded");
        stored.LinkedCardId.Should().Be(10, "the first card link wins; later skill runs do not relink");
    }

    [Fact]
    public async Task Handle_OverwritesExistingRun_OnSecondRun()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", ValidJson, "sha1"), default);

        const string smallerJson = """
            { "findings": [ { "title": "Only one", "body": "Just this.", "outcome": "parked" } ] }
            """;
        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", smallerJson, "sha2"),
            default);

        result.FindingCount.Should().Be(1);

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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
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
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        var act = () => sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, skillName, ValidJson, "sha"),
            default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*path separators*");
    }

    [Fact]
    public async Task Handle_EmptyFindingsArray_Succeeds()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
        const string json = """{ "findings": [] }""";

        var result = await sut.Handle(
            new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha"),
            default);

        result.FindingCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SecondRun_MergesFindings_PreservingMissingAsResolved()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", ValidJson, "sha1"), default);

        const string smallerJson = """
            { "findings": [ { "title": "Only one", "body": "Just this.", "outcome": "parked" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", smallerJson, "sha2"), default);

        var run = await _db.WorkspaceSkillRuns.AsNoTracking()
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch");
        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.WorkspaceSkillRunId == run.Id)
            .OrderBy(f => f.Title)
            .ToListAsync();

        findings.Should().HaveCount(3);
        findings.Single(f => f.Title == "Only one").Status.Should().Be("pending");
        findings.Where(f => f.Title != "Only one")
            .All(f => f.Status == "resolved")
            .Should().BeTrue("findings absent from the second run flip to resolved");
    }

    [Fact]
    public async Task Handle_SecondRun_PreservesRebuttalAndLinkedCardForMatchingFindings()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            {
              "findings": [
                {
                  "title": "Unused symbol",
                  "body": "x",
                  "outcome": "dismissed",
                  "file": "src/Foo.cs",
                  "rule": "SOLID/SRP",
                  "symbol": "Foo.Bar"
                }
              ]
            }
            """;

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha1"), default);

        // Dismiss it (simulating user action via WinUI).
        var run = await _db.WorkspaceSkillRuns
            .Include(r => r.Findings)
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch");
        var first = run.Findings.Single();
        var firstSeen = first.FirstSeenAt;
        first.Status = "dismissed";
        first.RebuttalText = "Not actually unused — reflected via DI.";
        first.LinkedCardId = 42;
        await _db.SaveChangesAsync();

        // Second run — same finding identity surfaces again.
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha2"), default);

        var merged = await _db.Findings.AsNoTracking()
            .SingleAsync(f => f.WorkspaceSkillRunId == run.Id);
        merged.Status.Should().Be("dismissed", "prior dismissal must survive a rerun");
        merged.RebuttalText.Should().Be("Not actually unused — reflected via DI.");
        merged.LinkedCardId.Should().Be(42);
        merged.FirstSeenAt.Should().Be(firstSeen, "FirstSeenAt must not move on a rerun");
        merged.LastSeenAt.Should().BeAfter(firstSeen, "LastSeenAt must update on each surfacing");
    }

    [Fact]
    public async Task Handle_ResolvedFindingRecurs_FlipsBackToPending()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            { "findings": [ { "title": "F", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha1"), default);
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", """{ "findings": [] }""", "sha2"), default);

        var resolved = await _db.Findings.AsNoTracking().SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        resolved.Status.Should().Be("resolved");

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha3"), default);

        var rerun = await _db.Findings.AsNoTracking().SingleAsync(f => f.Run.WorkspaceId == ws.Id);
        rerun.Status.Should().Be("pending", "a re-emerging resolved finding flips back to pending");
    }

    [Fact]
    public async Task Handle_PerProject_CreatesSeparateRuns()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string jsonProjectA = """
            {
              "projectName": "Bishop.App",
              "findings": [ { "title": "A finding", "body": "x", "outcome": "dismissed" } ]
            }
            """;
        const string jsonProjectB = """
            {
              "projectName": "Bishop.UI",
              "findings": [ { "title": "B finding", "body": "y", "outcome": "dismissed" } ]
            }
            """;

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-tests", jsonProjectA, "sha"), default);
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-tests", jsonProjectB, "sha"), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-tests")
            .OrderBy(r => r.ProjectName)
            .ToListAsync();
        runs.Should().HaveCount(2);
        runs[0].ProjectName.Should().Be("Bishop.App");
        runs[1].ProjectName.Should().Be("Bishop.UI");
    }

    [Fact]
    public async Task Handle_StructuredInputs_IdentityHashStableAcrossReruns()
    {
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            {
              "findings": [
                {
                  "title": "Unused symbol",
                  "body": "x",
                  "outcome": "dismissed",
                  "file": "src/Foo.cs",
                  "rule": "DEAD001",
                  "symbol": "Foo.Bar"
                }
              ]
            }
            """;

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-dead-code", json, "sha1"), default);
        var runId = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-dead-code")
            .Select(r => r.Id).SingleAsync();
        var firstHash = await _db.Findings.AsNoTracking()
            .Where(f => f.WorkspaceSkillRunId == runId)
            .Select(f => f.IdentityHash).SingleAsync();

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-dead-code", json, "sha2"), default);
        var secondHash = await _db.Findings.AsNoTracking()
            .Where(f => f.WorkspaceSkillRunId == runId)
            .Select(f => f.IdentityHash).SingleAsync();

        secondHash.Should().Be(firstHash);
    }

    [Fact]
    public void FindingIdentity_StructuredInputs_MatchSpec()
    {
        var hash = FindingIdentity.Compute("bish-dead-code", null, "src/Foo.cs", "DEAD001", "Foo.Bar", "ignored title");
        var legacy = FindingIdentity.Compute("bish-dead-code", null, null, null, null, "ignored title");
        hash.Should().NotBe(legacy);
        hash.Should().MatchRegex("^[0-9a-f]{40}$");
        legacy.Should().MatchRegex("^[0-9a-f]{40}$");
    }

    [Fact]
    public void FindingIdentity_PartialStructuredInputs_FallsBackToLegacyFormula()
    {
        var legacy = FindingIdentity.Compute("s", "p", null, null, null, "title");
        var partial = FindingIdentity.Compute("s", "p", "file", null, "symbol", "title");
        partial.Should().Be(legacy, "missing any of file/rule/symbol must fall back to the legacy formula");
    }

    [Fact]
    public async Task Handle_LegacyJsonPresent_ImportsAndDeletesOnFirstRun()
    {
        var ws = await CreateWorkspaceAsync();
        var findingsDir = Path.Combine(ws.Path, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);
        var legacyPath = Path.Combine(findingsDir, "bish-arch.json");
        const string legacyJson = """
            {
              "findings": [
                { "title": "Old A", "body": "a", "outcome": "dismissed" },
                { "title": "Old B", "body": "b", "outcome": "parked" }
              ]
            }
            """;
        await File.WriteAllTextAsync(legacyPath, legacyJson);

        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
        const string newJson = """
            { "findings": [ { "title": "New", "body": "n", "outcome": "dismissed" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", newJson, "sha"), default);

        File.Exists(legacyPath).Should().BeFalse("legacy json should be deleted after import");

        var run = await _db.WorkspaceSkillRuns.AsNoTracking()
            .SingleAsync(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-arch");
        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.WorkspaceSkillRunId == run.Id)
            .ToListAsync();
        findings.Single(f => f.Title == "New").Status.Should().Be("dismissed");
        findings.Where(f => f.Title != "New")
            .All(f => f.Status == "resolved")
            .Should().BeTrue("legacy-imported findings missing from the new payload flip to resolved");
    }

    [Fact]
    public async Task Handle_LegacyJsonPresent_SecondCallDoesNotReImport()
    {
        var ws = await CreateWorkspaceAsync();
        var findingsDir = Path.Combine(ws.Path, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);
        var legacyPath = Path.Combine(findingsDir, "bish-arch.json");
        await File.WriteAllTextAsync(legacyPath,
            """{ "findings": [ { "title": "Old", "body": "a", "outcome": "dismissed" } ] }""");

        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);
        const string newJson = """{ "findings": [ { "title": "New", "body": "n", "outcome": "dismissed" } ] }""";
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", newJson, "sha"), default);

        // Recreate the legacy file — handler should ignore it because runs now exist.
        await File.WriteAllTextAsync(legacyPath,
            """{ "findings": [ { "title": "Resurrected", "body": "r", "outcome": "dismissed" } ] }""");
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", newJson, "sha"), default);

        File.Exists(legacyPath).Should().BeTrue("legacy import is one-shot — the file should be left alone on subsequent runs");
    }
}

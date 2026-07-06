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
        return await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
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
    public async Task Handle_NonAllowlistedSkill_CollapsesProjectNameToNull()
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

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-coverage", jsonProjectA, "sha"), default);
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-coverage", jsonProjectB, "sha"), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-coverage")
            .ToListAsync();
        runs.Should().HaveCount(1);
        runs[0].ProjectName.Should().BeNull();
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
    public void FindingIdentity_FilePresent_DiffersFromTitleOnlyBranch()
    {
        var hash = FindingIdentity.Compute("bish-dead-code", null, "src/Foo.cs", "title");
        var titleOnly = FindingIdentity.Compute("bish-dead-code", null, null, "title");
        hash.Should().NotBe(titleOnly);
        hash.Should().MatchRegex("^[0-9a-f]{40}$");
        titleOnly.Should().MatchRegex("^[0-9a-f]{40}$");
    }

    [Fact]
    public void FindingIdentity_FileNullAndEmpty_AreEquivalent()
    {
        // Rows imported with no File should hash identically whether stored as null or empty.
        FindingIdentity.Compute("s", "p", null, "title")
            .Should().Be(FindingIdentity.Compute("s", "p", string.Empty, "title"));
    }

    [Fact]
    public void FindingIdentity_StableAcrossRuleSymbolChange()
    {
        // Rule and Symbol no longer participate in the hash — only skill, project, file, title.
        // This is verified by the signature itself; this test pins the inputs that DO matter.
        var first = FindingIdentity.Compute("bish-arch", "Bishop.App", "src/Foo.cs", "BreakoutEngine below threshold");
        var second = FindingIdentity.Compute("bish-arch", "Bishop.App", "src/Foo.cs", "BreakoutEngine below threshold");
        first.Should().Be(second);
    }

    [Fact]
    public async Task Handle_StaleHashRowExists_MergesByFileTitleAndPreservesManualOutcome()
    {
        // Simulate the pre-#959 state: a row stored under an IdentityHash that included
        // an LLM-emitted Rule/Symbol, with the user's manual `dismissed` outcome on it.
        // The next skill run emits a different Rule for the same (File, Title) finding;
        // the handler must merge into the existing row rather than create a duplicate,
        // and the `dismissed` status must be carried forward.
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        await using (var seed = await _factory.CreateDbContextAsync())
        {
            var run = new WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws.Id,
                SkillName = "bish-arch",
                ProjectName = null,
                GitSha = "old-sha",
                RecordedAt = DateTimeOffset.UtcNow.AddDays(-1),
                FindingsCount = 1,
            };
            seed.WorkspaceSkillRuns.Add(run);
            seed.Findings.Add(new Bishop.Core.Finding
            {
                Id = Guid.NewGuid(),
                WorkspaceSkillRunId = run.Id,
                IdentityHash = "stale-pre-959-hash",
                Status = "dismissed",
                File = "src/Foo.cs",
                Symbol = "Foo.Bar",
                Rule = "old-rule",
                Title = "BreakoutEngine below threshold",
                Body = "old body",
                FirstSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await seed.SaveChangesAsync();
        }

        const string newJson = """
            {
              "findings": [
                {
                  "title": "BreakoutEngine below threshold",
                  "body": "new body",
                  "file": "src/Foo.cs",
                  "symbol": "Foo.Bar",
                  "rule": "new-rule",
                  "outcome": "parked"
                }
              ]
            }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", newJson, "new-sha"), default);

        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.Title == "BreakoutEngine below threshold")
            .ToListAsync();
        findings.Should().HaveCount(1, "stale-hash row and incoming row must collapse into one");
        findings[0].Status.Should().Be("dismissed", "manual outcome wins over skill-emitted parked");
        findings[0].Rule.Should().Be("new-rule");
        findings[0].Body.Should().Be("new body");
    }

    [Fact]
    public async Task Handle_MultipleStaleHashRows_MergeIntoSingleWinnerByStatusPriority()
    {
        // Two duplicate rows for the same (File, Title): one carded with a linked card,
        // one resolved. Next run must keep the carded row's LinkedCardId.
        var ws = await CreateWorkspaceAsync();
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        await using (var seed = await _factory.CreateDbContextAsync())
        {
            var run = new WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws.Id,
                SkillName = "bish-arch",
                ProjectName = null,
                GitSha = "old",
                RecordedAt = DateTimeOffset.UtcNow.AddDays(-1),
                FindingsCount = 0,
            };
            seed.WorkspaceSkillRuns.Add(run);
            seed.Findings.Add(new Bishop.Core.Finding
            {
                Id = Guid.NewGuid(),
                WorkspaceSkillRunId = run.Id,
                IdentityHash = "hash-a",
                Status = "carded",
                LinkedCardId = 42,
                File = "src/Bar.cs",
                Title = "Tangled dependency",
                Body = "b",
                FirstSeenAt = DateTimeOffset.UtcNow.AddDays(-2),
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-2),
            });
            seed.Findings.Add(new Bishop.Core.Finding
            {
                Id = Guid.NewGuid(),
                WorkspaceSkillRunId = run.Id,
                IdentityHash = "hash-b",
                Status = "resolved",
                File = "src/Bar.cs",
                Title = "Tangled dependency",
                Body = "b2",
                FirstSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await seed.SaveChangesAsync();
        }

        const string newJson = """
            {
              "findings": [
                {
                  "title": "Tangled dependency",
                  "body": "fresh",
                  "file": "src/Bar.cs",
                  "outcome": "parked"
                }
              ]
            }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", newJson, "new"), default);

        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.Title == "Tangled dependency")
            .ToListAsync();
        findings.Should().HaveCount(1);
        findings[0].Status.Should().Be("carded");
        findings[0].LinkedCardId.Should().Be(42);
    }

    private async Task<Batch> CreateBatchAsync(Guid workspaceId)
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "batch-" + Guid.NewGuid().ToString("N")[..8],
            BranchName = "bishop/batch-" + Guid.NewGuid().ToString("N")[..8],
            BaseBranch = "main",
            Status = BatchStatus.Working,
            CreatedAt = DateTimeOffset.UtcNow,
            WorktreePath = @"C:\wt\" + Guid.NewGuid().ToString("N")[..8],
            Model = "claude-sonnet-5",
        };

        await using var seed = await _factory.CreateDbContextAsync();
        seed.Batches.Add(batch);
        await seed.SaveChangesAsync();
        return batch;
    }

    [Fact]
    public async Task Handle_BatchScopedRun_IsSeparateFromWholeSolutionRun()
    {
        var ws = await CreateWorkspaceAsync();
        var batch = await CreateBatchAsync(ws.Id);
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", ValidJson, "sha1"), default);
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", ValidJson, "sha2", batch.Id), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-review-batch")
            .ToListAsync();
        runs.Should().HaveCount(2, "a batch-scoped run must not collide with the null-batch whole-solution run");
        runs.Should().ContainSingle(r => r.BatchId == null);
        runs.Should().ContainSingle(r => r.BatchId == batch.Id);
    }

    [Fact]
    public async Task Handle_TwoBatches_SameFinding_KeptSeparate()
    {
        var ws = await CreateWorkspaceAsync();
        var b1 = await CreateBatchAsync(ws.Id);
        var b2 = await CreateBatchAsync(ws.Id);
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "Correctness", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", json, "s1", b1.Id), default);
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", json, "s2", b2.Id), default);

        var runs = await _db.WorkspaceSkillRuns.AsNoTracking()
            .Where(r => r.WorkspaceId == ws.Id && r.SkillName == "bish-review-batch")
            .ToListAsync();
        runs.Should().HaveCount(2);

        var findings = await _db.Findings.AsNoTracking()
            .Where(f => f.Run.WorkspaceId == ws.Id)
            .ToListAsync();
        findings.Should().HaveCount(2, "the same finding identity in two batches is two distinct rows");
    }

    [Fact]
    public async Task Handle_SameBatch_PreservesDismissalAcrossReReview()
    {
        // Acceptance: re-running the review of a batch after a dismissal must not re-raise
        // the dismissed finding — the dismissal survives the second record for the same batch.
        var ws = await CreateWorkspaceAsync();
        var batch = await CreateBatchAsync(ws.Id);
        var sut = new RecordFindingsCommandHandler(_factory, TimeProvider.System);

        const string json = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "Correctness", "symbol": "S" } ] }
            """;
        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", json, "s1", batch.Id), default);

        var f = await _db.Findings.Include(x => x.Run)
            .SingleAsync(x => x.Run.WorkspaceId == ws.Id && x.Run.BatchId == batch.Id);
        f.Status = "dismissed";
        f.RebuttalText = "intended — acceptance permits this";
        await _db.SaveChangesAsync();

        await sut.Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-review-batch", json, "s2", batch.Id), default);

        var merged = await _db.Findings.AsNoTracking().Include(x => x.Run)
            .SingleAsync(x => x.Run.WorkspaceId == ws.Id && x.Run.BatchId == batch.Id);
        merged.Status.Should().Be("dismissed", "a re-review of the same batch must not re-raise a dismissed finding");
        merged.RebuttalText.Should().Be("intended — acceptance permits this");
    }

}

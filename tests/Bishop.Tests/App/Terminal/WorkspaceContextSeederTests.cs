using Bishop.App.Terminal;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Terminal;

public sealed class WorkspaceContextSeederTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public WorkspaceContextSeederTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static Workspace MakeWorkspace(
        string name = "demo",
        string path = @"C:\demo",
        string? gitHubRepo = null,
        IEnumerable<(string Name, int Position, bool IsSystem)>? lanes = null,
        IEnumerable<string>? tags = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = path,
            GitHubRepo = gitHubRepo,
            Lanes = (lanes ?? [("To Do", 1, true), ("Doing", 2, true), ("Done", 3, true)])
                .Select(l => new Lane { Id = Guid.NewGuid(), Name = l.Name, Position = l.Position, IsSystem = l.IsSystem })
                .ToList(),
            Tags = (tags ?? ["bug", "feature"])
                .Select(t => new Tag { Id = Guid.NewGuid(), Name = t, Colour = "#888888" })
                .ToList(),
        };

    // ── BuildBishopContext ─────────────────────────────────────────────────────

    [Fact]
    public void BuildBishopContext_IncludesWorkspaceNameLanesAndTags()
    {
        var workspace = MakeWorkspace(
            name: "alpha",
            lanes: [("Ideas", 1, false), ("To Do", 2, true)],
            tags: ["bug", "chore"]);

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("# BISHOP_CONTEXT — alpha");
        output.Should().Contain("- **Name:** alpha");
        output.Should().Contain("1. Ideas");
        output.Should().Contain("2. To Do _(system)_");
        output.Should().Contain("- `bug`");
        output.Should().Contain("- `chore`");
    }

    [Fact]
    public void BuildBishopContext_IncludesGitHubLine_WhenRepoSet()
    {
        var workspace = MakeWorkspace(gitHubRepo: "owner/repo");

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("- **GitHub:** `owner/repo`");
    }

    [Fact]
    public void BuildBishopContext_OmitsGitHubLine_WhenRepoNotSet()
    {
        var workspace = MakeWorkspace(gitHubRepo: null);

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().NotContain("**GitHub:**");
    }

    [Fact]
    public void BuildBishopContext_IncludesCardModelAndCliReference()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Card model");
        output.Should().Contain("`#N`");
        output.Should().Contain("## CLI quick reference");
        output.Should().Contain("`bishop card add");
        output.Should().Contain("`bishop card claim");
    }

    [Fact]
    public void BuildBishopContext_CardBodyConvention_DescribesH3SectionFormat()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("### Card body convention");
        output.Should().Contain("`### Why`");
        output.Should().Contain("`### Acceptance`");
        output.Should().Contain("`### Changes`");
        output.Should().Contain("`### Decided`");
        output.Should().Contain("--description-file -");
        output.Should().Contain("### Why");
        output.Should().Contain("### Acceptance");
    }

    [Fact]
    public void BuildBishopContext_OmitsDestructiveCommands()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().NotContain("card remove");
        output.Should().NotContain("lane remove");
        output.Should().NotContain("tag remove");
    }

    [Fact]
    public void BuildBishopContext_IncludesWorkflowSection_WithSkillRoles()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Workflow");
        output.Should().Contain("`bish-grill-me`");
        output.Should().Contain("`bish-work-on-card`");
        output.Should().Contain("`bish-auto-card`");
        output.Should().Contain("`bish-arch`");
        output.Should().Contain("`bish-coverage`");
        output.Should().Contain("`bish-tests`");
        output.Should().Contain("`bish-audit-docs`");
    }

    [Fact]
    public void BuildBishopContext_DistinguishesGrillMeFromWorkOnCard()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("Choosing between `bish-grill-me` and `bish-work-on-card`");
    }

    [Fact]
    public void BuildBishopContext_DescribesCardPushFlow()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Publishing cards to GitHub");
        output.Should().Contain("bishop card push <number>");
        output.Should().Contain("on-demand");
    }

    [Fact]
    public void BuildBishopContext_DocumentsCommitReferenceConvention()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Commit-reference convention");
        output.Should().Contain("(card #N)");
        output.Should().Contain("(card #42)");
    }

    [Fact]
    public void BuildBishopContext_OrdersWorkflowBeforeCardModel()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.IndexOf("## Workflow", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("## Card model", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildBishopContext_StillIncludesLiveWorkspaceMetadata_AlongsideStaticBody()
    {
        var workspace = MakeWorkspace(
            name: "epsilon",
            gitHubRepo: "owner/epsilon",
            lanes: [("To Do", 1, true), ("Doing", 2, true)],
            tags: ["bug"]);

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.IndexOf("# BISHOP_CONTEXT — epsilon", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("## Workflow", StringComparison.Ordinal));
        output.Should().Contain("- **GitHub:** `owner/epsilon`");
        output.Should().Contain("1. To Do _(system)_");
        output.Should().Contain("- `bug`");
    }

    [Fact]
    public void BuildBishopContext_RendersEmptyFallbacks_WhenNoLanesOrTags()
    {
        var workspace = MakeWorkspace(lanes: [], tags: []);

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("_No lanes yet._");
        output.Should().Contain("_No tags yet._");
    }

    [Fact]
    public void LoadStaticBody_ReturnsNonEmptyString()
    {
        var body = WorkspaceContextSeeder.LoadStaticBody();

        body.Should().NotBeNullOrEmpty();
        body.Should().Contain("## Card model");
        body.Should().Contain("## CLI quick reference");
    }

    [Fact]
    public void LoadStaticBody_NormalizesLineEndingsToEnvironmentNewLine()
    {
        var body = WorkspaceContextSeeder.LoadStaticBody();

        var stripped = body.Replace(Environment.NewLine, string.Empty);
        stripped.Should().NotContain("\n");
        stripped.Should().NotContain("\r");
    }

    // ── EnsureContextMd ────────────────────────────────────────────────────────

    [Fact]
    public void EnsureContextMd_ReturnsStub_WhenExistingNull()
    {
        var workspace = MakeWorkspace(name: "alpha");

        var output = WorkspaceContextSeeder.EnsureContextMd(null, workspace);

        output.Should().Contain("# alpha");
        output.Should().Contain("BISHOP_CONTEXT.md");
    }

    [Fact]
    public void EnsureContextMd_ReturnsExistingUnchanged_WhenPointerAlreadyPresent()
    {
        var workspace = MakeWorkspace();
        var existing = "# Existing\r\n\r\n> See [BISHOP_CONTEXT.md](./BISHOP_CONTEXT.md) for stuff.\r\n\r\nbody\r\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().Be(existing);
    }

    [Fact]
    public void EnsureContextMd_InsertsPointerAfterH1_WhenH1Present()
    {
        var workspace = MakeWorkspace();
        var existing = "# My Project\n\nIntro paragraph.\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().Contain("# My Project");
        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().Contain("Intro paragraph.");
        output.IndexOf("# My Project", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf(WorkspaceContextSeeder.PointerLine, StringComparison.Ordinal));
        output.IndexOf(WorkspaceContextSeeder.PointerLine, StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Intro paragraph.", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureContextMd_InsertsPointerAtTop_WhenNoH1()
    {
        var workspace = MakeWorkspace();
        var existing = "Just a paragraph with no heading.\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().StartWith(WorkspaceContextSeeder.PointerLine);
        output.Should().Contain("Just a paragraph with no heading.");
    }

    [Fact]
    public void EnsureContextMd_PreservesCrLfLineEndings()
    {
        var workspace = MakeWorkspace();
        var existing = "# Project\r\n\r\nIntro.\r\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().Contain("\r\n");
        output.Should().NotContain("\n\n\n"); // no LF-only gaps after splitting on \n
    }

    [Fact]
    public void EnsureContextMd_PreservesLfLineEndings()
    {
        var workspace = MakeWorkspace();
        var existing = "# Project\n\nIntro.\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().NotContain("\r\n");
    }

    // ── EnsureClaudeMd ─────────────────────────────────────────────────────────

    [Fact]
    public void EnsureClaudeMd_ReturnsStub_WhenExistingNull()
    {
        var workspace = MakeWorkspace(name: "alpha");

        var output = WorkspaceContextSeeder.EnsureClaudeMd(null, workspace);

        output.Should().Contain("# alpha");
        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
    }

    [Fact]
    public void EnsureClaudeMd_ReturnsExistingUnchanged_WhenPointerAlreadyPresent()
    {
        var workspace = MakeWorkspace();
        var existing = "# Existing\r\n\r\n> See [BISHOP_CONTEXT.md](./BISHOP_CONTEXT.md) for stuff.\r\n\r\nbody\r\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().Be(existing);
    }

    [Fact]
    public void EnsureClaudeMd_InsertsPointerAfterH1_WhenH1Present()
    {
        var workspace = MakeWorkspace();
        var existing = "# My Project\n\nClaude-specific notes.\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().Contain("# My Project");
        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().Contain("Claude-specific notes.");
        output.IndexOf("# My Project", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf(WorkspaceContextSeeder.PointerLine, StringComparison.Ordinal));
        output.IndexOf(WorkspaceContextSeeder.PointerLine, StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Claude-specific notes.", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureClaudeMd_InsertsPointerAtTop_WhenNoH1()
    {
        var workspace = MakeWorkspace();
        var existing = "Just a paragraph with no heading.\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().StartWith(WorkspaceContextSeeder.PointerLine);
        output.Should().Contain("Just a paragraph with no heading.");
    }

    [Fact]
    public void EnsureClaudeMd_PreservesCrLfLineEndings()
    {
        var workspace = MakeWorkspace();
        var existing = "# Project\r\n\r\nIntro.\r\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().Contain("\r\n");
        output.Should().NotContain("\n\n\n");
    }

    [Fact]
    public void EnsureClaudeMd_PreservesLfLineEndings()
    {
        var workspace = MakeWorkspace();
        var existing = "# Project\n\nIntro.\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().NotContain("\r\n");
    }

    // ── SeedAsync (integration with DbFixture + temp dir) ──────────────────────

    [Fact]
    public async Task SeedAsync_WritesAllThreeFiles_WhenWorkspaceResolved()
    {
        var temp = CreateTempDir();
        try
        {
            var name = U("Alpha");
            await SeedRegisteredWorkspaceAsync(temp, name: name);

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);

            var bishopContent = File.ReadAllText(Path.Combine(temp, "BISHOP_CONTEXT.md"));
            bishopContent.Should().Contain($"# BISHOP_CONTEXT — {name}");
            bishopContent.Should().Contain("## Card model");
            bishopContent.Should().Contain("## CLI quick reference");

            var contextContent = File.ReadAllText(Path.Combine(temp, "CONTEXT.md"));
            contextContent.Should().Contain("BISHOP_CONTEXT.md");

            var claudeContent = File.ReadAllText(Path.Combine(temp, "CLAUDE.md"));
            claudeContent.Should().Contain($"# {name}");
            claudeContent.Should().Contain("BISHOP_CONTEXT.md");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenPathIsWhitespaceOnly()
    {
        var sut = new WorkspaceContextSeeder(_factory);

        await sut.SeedAsync("   ");
        // Covers the IsNullOrWhiteSpace early-return guard — no exception, no files written.
    }

    [Fact]
    public void BishopContextStaticResource_IsPresent_InAssemblyManifest()
    {
        var assembly = typeof(WorkspaceContextSeeder).Assembly;

        using var stream = assembly.GetManifestResourceStream("Bishop.App.Terminal.BishopContext.static.md");

        stream.Should().NotBeNull("the embedded BishopContext.static.md resource must exist in the assembly manifest");
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenPathDoesNotExist()
    {
        var sut = new WorkspaceContextSeeder(_factory);

        await sut.SeedAsync(@"C:\definitely-not-a-real-path-" + Guid.NewGuid().ToString("N"));
        // No exception, no files written, no DB query needed beyond a no-op.
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspaceNotRegistered()
    {
        var temp = CreateTempDir();
        try
        {
            var sut = new WorkspaceContextSeeder(_factory);

            await sut.SeedAsync(temp);

            File.Exists(Path.Combine(temp, "BISHOP_CONTEXT.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "CONTEXT.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "CLAUDE.md")).Should().BeFalse();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PreservesUserClaudeMd_AndInjectsPointer()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Zeta"));
            var claudePath = Path.Combine(temp, "CLAUDE.md");
            var original = "# Zeta Project\r\n\r\nProject-specific Claude Code instructions.\r\n";
            File.WriteAllText(claudePath, original);

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(claudePath);
            updated.Should().Contain("Project-specific Claude Code instructions.");
            updated.Should().Contain("BISHOP_CONTEXT.md");
            updated.Should().Contain("# Zeta Project");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_RoundTrip_LeavesClaudeMdUnchanged_WhenPointerAlreadyPresent()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Eta"));
            var claudePath = Path.Combine(temp, "CLAUDE.md");

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);
            var firstPass = File.ReadAllText(claudePath);

            await sut.SeedAsync(temp);
            var secondPass = File.ReadAllText(claudePath);

            secondPass.Should().Be(firstPass);
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PreservesUserContextMd_AndInjectsPointer()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Beta"));
            var contextPath = Path.Combine(temp, "CONTEXT.md");
            var original = "# Beta Project\r\n\r\nHand-written intro about this workspace.\r\n";
            File.WriteAllText(contextPath, original);

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(contextPath);
            updated.Should().Contain("Hand-written intro about this workspace.");
            updated.Should().Contain("BISHOP_CONTEXT.md");
            updated.Should().Contain("# Beta Project");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_RoundTrip_LeavesContextMdUnchanged_WhenPointerAlreadyPresent()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Gamma"));
            var contextPath = Path.Combine(temp, "CONTEXT.md");

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);
            var firstPass = File.ReadAllText(contextPath);

            await sut.SeedAsync(temp);
            var secondPass = File.ReadAllText(contextPath);

            secondPass.Should().Be(firstPass);
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_BishopContext_ReflectsAddedTagOnNextLaunch()
    {
        var temp = CreateTempDir();
        try
        {
            var workspace = await SeedRegisteredWorkspaceAsync(temp, name: U("Delta"));

            var sut = new WorkspaceContextSeeder(_factory);
            await sut.SeedAsync(temp);
            var firstPass = File.ReadAllText(Path.Combine(temp, "BISHOP_CONTEXT.md"));
            firstPass.Should().NotContain("`newtag`");

            _db.Tags.Add(new Tag { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "newtag", Colour = "#888888" });
            await _db.SaveChangesAsync();

            await sut.SeedAsync(temp);
            var secondPass = File.ReadAllText(Path.Combine(temp, "BISHOP_CONTEXT.md"));

            secondPass.Should().Contain("- `newtag`");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Workspace> SeedRegisteredWorkspaceAsync(string path, string name)
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = path,
            Position = 1,
        };
        _db.Workspaces.Add(workspace);
        _db.Lanes.Add(new Lane { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "To Do", Position = 1, IsSystem = true });
        _db.Lanes.Add(new Lane { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "Doing", Position = 2, IsSystem = true });
        _db.Tags.Add(new Tag { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "feature", Colour = "#888888" });
        await _db.SaveChangesAsync();
        return workspace;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bishop-ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}

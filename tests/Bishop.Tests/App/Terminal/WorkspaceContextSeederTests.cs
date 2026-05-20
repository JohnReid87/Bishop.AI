using Bishop.App.Terminal;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;

namespace Bishop.Tests.App.Terminal;

public sealed class WorkspaceContextSeederTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;

    public WorkspaceContextSeederTests(DbFixture fixture)
    {
        _db = fixture.Db;
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
    public void BuildBishopContext_OmitsDestructiveCommandsAndSkillCatalog()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().NotContain("card remove");
        output.Should().NotContain("lane remove");
        output.Should().NotContain("tag remove");
        output.Should().NotContain("bish-work-on-card");
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

    // ── SeedAsync (integration with DbFixture + temp dir) ──────────────────────

    [Fact]
    public async Task SeedAsync_WritesBothFiles_WhenWorkspaceResolved()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Alpha"));

            var sut = new WorkspaceContextSeeder(_db);
            await sut.SeedAsync(temp);

            File.Exists(Path.Combine(temp, "BISHOP_CONTEXT.md")).Should().BeTrue();
            File.Exists(Path.Combine(temp, "CONTEXT.md")).Should().BeTrue();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenPathDoesNotExist()
    {
        var sut = new WorkspaceContextSeeder(_db);

        await sut.SeedAsync(@"C:\definitely-not-a-real-path-" + Guid.NewGuid().ToString("N"));
        // No exception, no files written, no DB query needed beyond a no-op.
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspaceNotRegistered()
    {
        var temp = CreateTempDir();
        try
        {
            var sut = new WorkspaceContextSeeder(_db);

            await sut.SeedAsync(temp);

            File.Exists(Path.Combine(temp, "BISHOP_CONTEXT.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "CONTEXT.md")).Should().BeFalse();
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

            var sut = new WorkspaceContextSeeder(_db);
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

            var sut = new WorkspaceContextSeeder(_db);
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

            var sut = new WorkspaceContextSeeder(_db);
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

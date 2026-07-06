using Bishop.App.Services.Terminal;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Bishop.Tests.App.Services.Terminal;

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
        string path = @"C:\demo") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = path,
        };

    // ── BuildBishopContext ─────────────────────────────────────────────────────

    [Fact]
    public void BuildBishopContext_IncludesWorkspaceName()
    {
        var workspace = MakeWorkspace(name: "alpha");

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("# BISHOP_CONTEXT — alpha");
        output.Should().Contain("- **Name:** alpha");
    }

    [Fact]
    public void BuildBishopContext_RendersSystemLanesFromConstants_InOrder()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("1. Backlog");
        output.Should().Contain("2. To Do");
        output.Should().Contain("3. Doing");
        output.Should().Contain("4. Done");
        output.Should().Contain($"1. Backlog{Environment.NewLine}");
        output.Should().Contain($"2. To Do{Environment.NewLine}");
        output.Should().Contain($"3. Doing{Environment.NewLine}");
        output.Should().Contain($"4. Done{Environment.NewLine}");
    }

    [Fact]
    public void BuildBishopContext_RendersBrandTagsFromConstants()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        foreach (var tag in BrandTagPalette.DefaultColours.Keys)
            output.Should().Contain($"- `{tag}`");
    }

    [Fact]
    public void BuildBishopContext_IncludesCardModelAndCliReference()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Card model");
        output.Should().Contain("`#N`");
        output.Should().Contain("## CLI quick reference");
        output.Should().Contain("`bishop card create");
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
        output.Should().Contain("--description-file");
        output.Should().Contain(".bishop/");
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
        output.Should().Contain("`bish-grill-cards`");
        output.Should().Contain("`bish-work-on-card`");
        output.Should().Contain("`bish-auto-card`");
        output.Should().Contain("`bish-arch`");
        output.Should().Contain("`bish-coverage`");
        output.Should().Contain("`bish-tests`");
        output.Should().Contain("`bish-security`");
        output.Should().Contain("`bish-audit-docs`");
    }

    [Fact]
    public void BuildBishopContext_DistinguishesGrillCardsFromWorkOnCard()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("Choosing between `bish-spec-cards`, `bish-grill-cards`, and `bish-work-on-card`");
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
    public void BuildBishopContext_IncludesAutoCardPermissionContract()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Auto-card permission contract");
        output.Should().Contain(".claude/settings.json");
        output.Should().Contain("bypassPermissions");
        output.Should().Contain("bishop hook check-path");
        output.Should().Contain("git push");
        output.Should().Contain("gh:*");
    }

    [Fact]
    public void BuildBishopContext_AutoCardPermissionContract_AppearsAfterCommitConventionAndBeforeCardModel()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        var lines = output.Split('\n');
        var commitLine = Array.FindIndex(lines, l => l.Contains("## Commit-reference convention"));
        var contractLine = Array.FindIndex(lines, l => l.Contains("## Auto-card permission contract"));
        var cardModelLine = Array.FindIndex(lines, l => l.Contains("## Card model"));

        contractLine.Should().BeGreaterThan(commitLine);
        contractLine.Should().BeLessThan(cardModelLine);
    }

    [Fact]
    public void BuildBishopContext_IncludesSkillConventionsIntro_WithStableAndTunableLabels()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Skill conventions");
        output.Should().Contain("**STABLE**");
        output.Should().Contain("**TUNABLE**");
        output.Should().Contain("docs/SKILL_FAMILY.md");
    }

    [Fact]
    public void BuildBishopContext_IncludesFiveCanonicalSkillSections_WithCorrectLabels()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Card Push Procedure (STABLE)");
        output.Should().Contain("## Task List Preview Format (STABLE)");
        output.Should().Contain("## Source Card Closing Prompt (STABLE)");
        output.Should().Contain("## Card Granularity Rules (TUNABLE)");
        output.Should().Contain("## Per-finding Walk Pattern (TUNABLE)");
    }

    [Fact]
    public void BuildBishopContext_CardPushProcedure_DocumentsTempFileFlow()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Card Push Procedure (STABLE)");
        output.Should().Contain("--description-file");
        output.Should().Contain("--bottom");
        output.Should().Contain(".bishop/tmp-card-");
        output.Should().Contain("Remove-Item");
        output.Should().Contain("PowerShell");
    }

    [Fact]
    public void BuildBishopContext_TaskListPreviewFormat_DocumentsH3CardsAndTagLaneLine()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Task List Preview Format (STABLE)");
        output.Should().Contain("**Tag:**");
        output.Should().Contain("**Lane:**");
        output.Should().Contain("#### Why");
        output.Should().Contain("#### Acceptance");
    }

    [Fact]
    public void BuildBishopContext_SourceCardClosingPrompt_DocumentsAllThreeOptions()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        output.Should().Contain("## Source Card Closing Prompt (STABLE)");
        output.Should().Contain("`close`");
        output.Should().Contain("`done`");
        output.Should().Contain("`leave`");
        output.Should().Contain("bishop card close <number>");
        output.Should().Contain("bishop card move <number> --to-lane \"Done\"");
    }

    [Fact]
    public void BuildBishopContext_SkillConventions_AppearAfterCliQuickReference()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        var lines = output.Split('\n');
        var cliLine = Array.FindIndex(lines, l => l.Contains("## CLI quick reference"));
        var conventionsLine = Array.FindIndex(lines, l => l.Contains("## Skill conventions"));
        var pushLine = Array.FindIndex(lines, l => l.Contains("## Card Push Procedure (STABLE)"));

        cliLine.Should().BeGreaterThan(-1);
        conventionsLine.Should().BeGreaterThan(cliLine);
        pushLine.Should().BeGreaterThan(conventionsLine);
    }

    [Fact]
    public void BuildBishopContext_OrdersWorkflowBeforeCardModel()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        var lines = output.Split('\n');
        var workflowLine = Array.FindIndex(lines, l => l.Contains("## Workflow"));
        var cardModelLine = Array.FindIndex(lines, l => l.Contains("## Card model"));

        workflowLine.Should().BeLessThan(cardModelLine);
    }

    [Fact]
    public void BuildBishopContext_OrdersThisWorkspaceBeforeLanesBeforeTags()
    {
        var workspace = MakeWorkspace();

        var output = WorkspaceContextSeeder.BuildBishopContext(workspace);

        var lines = output.Split('\n');
        var thisWorkspaceLine = Array.FindIndex(lines, l => l.TrimEnd('\r') == "## This workspace");
        var lanesLine = Array.FindIndex(lines, l => l.TrimEnd('\r') == "### Lanes");
        var tagsLine = Array.FindIndex(lines, l => l.TrimEnd('\r') == "### Tags");

        thisWorkspaceLine.Should().BeLessThan(lanesLine);
        lanesLine.Should().BeLessThan(tagsLine);
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

        body.Should().Contain(Environment.NewLine);
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
        var existing = "# Existing\r\n\r\n> See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) for stuff.\r\n\r\nbody\r\n";

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
        var lines = output.Split('\n');
        var h1Line = Array.FindIndex(lines, l => l.Contains("# My Project"));
        var pointerLine = Array.FindIndex(lines, l => l.Contains(WorkspaceContextSeeder.PointerLine));
        var introLine = Array.FindIndex(lines, l => l.Contains("Intro paragraph."));

        h1Line.Should().BeLessThan(pointerLine);
        pointerLine.Should().BeLessThan(introLine);
    }

    [Fact]
    public void EnsureContextMd_InsertsPointerAfterFirstH1_WhenMultipleH1sPresent()
    {
        var workspace = MakeWorkspace();
        var existing = "# First Header\n\nContent under first.\n\n# Second Header\n\nContent under second.\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        var lines = output.Split('\n');
        var pointerLineIdx = Array.FindIndex(lines, l => l.Contains(WorkspaceContextSeeder.PointerLine));
        var firstH1LineIdx = Array.FindIndex(lines, l => l.Contains("# First Header"));
        var secondH1LineIdx = Array.FindIndex(lines, l => l.Contains("# Second Header"));

        pointerLineIdx.Should().BeGreaterThan(firstH1LineIdx, "pointer must follow the first H1");
        pointerLineIdx.Should().BeLessThan(secondH1LineIdx, "pointer must not be pushed past the second H1");
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
        output.Should().NotContain("\n\n\n");
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
        var existing = "# Existing\r\n\r\n> See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) for stuff.\r\n\r\nbody\r\n";

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
        var lines = output.Split('\n');
        var h1Line = Array.FindIndex(lines, l => l.Contains("# My Project"));
        var pointerLine = Array.FindIndex(lines, l => l.Contains(WorkspaceContextSeeder.PointerLine));
        var notesLine = Array.FindIndex(lines, l => l.Contains("Claude-specific notes."));

        h1Line.Should().BeLessThan(pointerLine);
        pointerLine.Should().BeLessThan(notesLine);
    }

    [Fact]
    public void EnsureClaudeMd_InsertsPointerAfterFirstH1_WhenMultipleH1sPresent()
    {
        var workspace = MakeWorkspace();
        var existing = "# First Header\n\nContent under first.\n\n# Second Header\n\nContent under second.\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        var lines = output.Split('\n');
        var pointerLineIdx = Array.FindIndex(lines, l => l.Contains(WorkspaceContextSeeder.PointerLine));
        var firstH1LineIdx = Array.FindIndex(lines, l => l.Contains("# First Header"));
        var secondH1LineIdx = Array.FindIndex(lines, l => l.Contains("# Second Header"));

        pointerLineIdx.Should().BeGreaterThan(firstH1LineIdx, "pointer must follow the first H1");
        pointerLineIdx.Should().BeLessThan(secondH1LineIdx, "pointer must not be pushed past the second H1");
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

    // ── MigrateLegacyFiles ────────────────────────────────────────────────────

    [Fact]
    public void MigrateLegacyFiles_MovesLegacyBishopContextMd()
    {
        var temp = CreateTempDir();
        try
        {
            var bishopDir = Path.Combine(temp, WorkspaceContextSeeder.BishopFolder);
            var legacyFile = Path.Combine(temp, WorkspaceContextSeeder.BishopContextFileName);
            File.WriteAllText(legacyFile, "legacy content");

            WorkspaceContextSeeder.MigrateLegacyFiles(temp, bishopDir);

            File.Exists(legacyFile).Should().BeFalse("legacy root file must be moved");
            File.Exists(Path.Combine(bishopDir, WorkspaceContextSeeder.BishopContextFileName)).Should().BeTrue("file must be in .bishop/");
            File.ReadAllText(Path.Combine(bishopDir, WorkspaceContextSeeder.BishopContextFileName)).Should().Be("legacy content", "content must be preserved");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public void MigrateLegacyFiles_MovesLegacyNotesMd()
    {
        var temp = CreateTempDir();
        try
        {
            var bishopDir = Path.Combine(temp, WorkspaceContextSeeder.BishopFolder);
            var legacyNotes = Path.Combine(temp, "BISHOP_NOTES.md");
            File.WriteAllText(legacyNotes, "my notes");

            WorkspaceContextSeeder.MigrateLegacyFiles(temp, bishopDir);

            File.Exists(legacyNotes).Should().BeFalse("legacy root notes file must be moved");
            File.Exists(Path.Combine(bishopDir, "BISHOP_NOTES.md")).Should().BeTrue("notes must be in .bishop/");
            File.ReadAllText(Path.Combine(bishopDir, "BISHOP_NOTES.md")).Should().Be("my notes", "content must be preserved");
        }
        finally
        {
            CleanupTempDir(temp);
        }
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

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            var bishopContent = File.ReadAllText(Path.Combine(temp, ".bishop", "BISHOP_CONTEXT.md"));
            bishopContent.Should().Contain($"# BISHOP_CONTEXT — {name}");
            bishopContent.Should().Contain("## Card model");
            bishopContent.Should().Contain("## CLI quick reference");

            var contextContent = File.ReadAllText(Path.Combine(temp, "CONTEXT.md"));
            contextContent.Should().Contain(".bishop/BISHOP_CONTEXT.md");

            var claudeContent = File.ReadAllText(Path.Combine(temp, "CLAUDE.md"));
            claudeContent.Should().Contain($"# {name}");
            claudeContent.Should().Contain(".bishop/BISHOP_CONTEXT.md");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenPathIsWhitespaceOnly()
    {
        const string path = "   ";
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        await sut.SeedAsync(path);

        File.Exists(Path.Combine(path, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(path, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(path, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
    }

    [Fact]
    public void BishopContextStaticResource_IsPresent_InAssemblyManifest()
    {
        var assembly = typeof(WorkspaceContextSeeder).Assembly;

        using var stream = assembly.GetManifestResourceStream("Bishop.App.Services.Terminal.BishopContext.static.md");

        stream.Should().NotBeNull("the embedded BishopContext.static.md resource must exist in the assembly manifest");
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenPathDoesNotExist()
    {
        var path = @"C:\definitely-not-a-real-path-" + Guid.NewGuid().ToString("N");
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        await sut.SeedAsync(path);

        File.Exists(Path.Combine(path, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(path, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(path, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenRegisteredWorkspacePathDoesNotExist()
    {
        // path is not whitespace but directory does not exist — kills the ||→&& logical mutant on line 25:
        // with &&, the guard evaluates false (IsNullOrWhiteSpace=false && !Exists=true → false), the method
        // proceeds to the DB, finds the workspace, and writes files — making the assertions below fail.
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "bishop-ctx-missing-" + Guid.NewGuid().ToString("N"));
        await SeedRegisteredWorkspaceAsync(nonExistentPath, name: U("missing"));
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        try
        {
            await sut.SeedAsync(nonExistentPath);

            Directory.Exists(Path.Combine(nonExistentPath, WorkspaceContextSeeder.BishopFolder)).Should().BeFalse();
            File.Exists(Path.Combine(nonExistentPath, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
            File.Exists(Path.Combine(nonExistentPath, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
        }
        finally
        {
            CleanupTempDir(nonExistentPath);
        }
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspaceNotRegistered()
    {
        var temp = CreateTempDir();
        try
        {
            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

            await sut.SeedAsync(temp);

            File.Exists(Path.Combine(temp, ".bishop", "BISHOP_CONTEXT.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "CONTEXT.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "CLAUDE.md")).Should().BeFalse();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    // ── IsShallowOrSensitivePath ──────────────────────────────────────────────

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForDriveRoot()
    {
        var root = Path.TrimEndingDirectorySeparator(
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!);

        WorkspaceContextSeeder.IsShallowOrSensitivePath(root).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForUserProfile()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        WorkspaceContextSeeder.IsShallowOrSensitivePath(userProfile).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForWindowsDirectory()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        WorkspaceContextSeeder.IsShallowOrSensitivePath(winDir).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsFalse_ForNormalProjectPath()
    {
        var projectPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "source", "repos", "MyProject");

        WorkspaceContextSeeder.IsShallowOrSensitivePath(projectPath).Should().BeFalse();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForWindowsSubdirectory()
    {
        var winSubDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "drivers");

        WorkspaceContextSeeder.IsShallowOrSensitivePath(winSubDir).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForProgramFilesSubdirectory()
    {
        var pfSubDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SomeApp");

        WorkspaceContextSeeder.IsShallowOrSensitivePath(pfSubDir).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForAppDataRoamingRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        WorkspaceContextSeeder.IsShallowOrSensitivePath(appData).Should().BeTrue();
    }

    [Fact]
    public void IsShallowOrSensitivePath_ReturnsTrue_ForAppDataRoamingSubdirectory()
    {
        var appDataSubDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SomeApp");

        WorkspaceContextSeeder.IsShallowOrSensitivePath(appDataSubDir).Should().BeTrue();
    }

    // ── SeedAsync — shallow / sensitive path guard ────────────────────────────

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspacePathIsDriveRoot()
    {
        var driveRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        await SeedRegisteredWorkspaceAsync(driveRoot, name: U("DriveRoot"));
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        await sut.SeedAsync(driveRoot);

        File.Exists(Path.Combine(driveRoot, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(driveRoot, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(driveRoot, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspacePathIsUserProfileRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await SeedRegisteredWorkspaceAsync(userProfile, name: U("UserProfile"));
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        await sut.SeedAsync(userProfile);

        File.Exists(Path.Combine(userProfile, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(userProfile, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(userProfile, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenWorkspacePathIsWindowsDirectory()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        await SeedRegisteredWorkspaceAsync(winDir, name: U("WinDir"));
        var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

        await sut.SeedAsync(winDir);

        File.Exists(Path.Combine(winDir, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(winDir, WorkspaceContextSeeder.ContextFileName)).Should().BeFalse();
        File.Exists(Path.Combine(winDir, WorkspaceContextSeeder.ClaudeMdFileName)).Should().BeFalse();
    }

    // ── SeedAsync — path normalisation (ResolveWorkspaceAsync) ───────────────

    [Fact]
    public async Task SeedAsync_SeedsFiles_WhenPathHasTrailingSlash()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("TrailSlash"));

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp + Path.DirectorySeparatorChar);

            File.Exists(Path.Combine(temp, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeTrue();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_SeedsFiles_WhenPathUsesMixedSeparators()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("MixedSep"));
            var mixedPath = temp.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(mixedPath);

            File.Exists(Path.Combine(temp, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeTrue();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_SeedsFiles_WhenPathDiffersByCase()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("CaseVar"));

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp.ToUpperInvariant());

            File.Exists(Path.Combine(temp, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName)).Should().BeTrue();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PropagatesException_WhenBishopContextFileIsLocked()
    {
        var temp = CreateTempDir();
        FileStream? lockStream = null;
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Kappa"));
            var bishopDir = Path.Combine(temp, WorkspaceContextSeeder.BishopFolder);
            Directory.CreateDirectory(bishopDir);
            var bishopFile = Path.Combine(bishopDir, WorkspaceContextSeeder.BishopContextFileName);
            lockStream = File.Open(bishopFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            Func<Task> act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            lockStream?.Dispose();
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PropagatesException_WhenContextFileIsLocked()
    {
        var temp = CreateTempDir();
        FileStream? lockStream = null;
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Kappa"));
            var contextFile = Path.Combine(temp, WorkspaceContextSeeder.ContextFileName);
            lockStream = File.Open(contextFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            Func<Task> act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            lockStream?.Dispose();
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PropagatesException_WhenClaudeMdFileIsLocked()
    {
        var temp = CreateTempDir();
        FileStream? lockStream = null;
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Kappa"));
            var claudeFile = Path.Combine(temp, WorkspaceContextSeeder.ClaudeMdFileName);
            lockStream = File.Open(claudeFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            Func<Task> act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            lockStream?.Dispose();
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

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(claudePath);
            updated.Should().Contain("Project-specific Claude Code instructions.");
            updated.Should().Contain(".bishop/BISHOP_CONTEXT.md");
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

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
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

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(contextPath);
            updated.Should().Contain("Hand-written intro about this workspace.");
            updated.Should().Contain(".bishop/BISHOP_CONTEXT.md");
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

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
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

    // ── EnsureContextMd / EnsureClaudeMd — legacy pointer rewrite ─────────────

    [Fact]
    public void EnsureContextMd_RewritesLegacyPointerLine_WhenOldPathPresent()
    {
        var workspace = MakeWorkspace();
        var existing = $"# Project\n\n{WorkspaceContextSeeder.LegacyPointerLine}\n\nBody.\n";

        var output = WorkspaceContextSeeder.EnsureContextMd(existing, workspace);

        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().NotContain(WorkspaceContextSeeder.LegacyPointerLine);
    }

    [Fact]
    public void EnsureClaudeMd_RewritesLegacyPointerLine_WhenOldPathPresent()
    {
        var workspace = MakeWorkspace();
        var existing = $"# Project\n\n{WorkspaceContextSeeder.LegacyPointerLine}\n";

        var output = WorkspaceContextSeeder.EnsureClaudeMd(existing, workspace);

        output.Should().Contain(WorkspaceContextSeeder.PointerLine);
        output.Should().NotContain(WorkspaceContextSeeder.LegacyPointerLine);
    }

    // ── SeedAsync — migration ─────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_MigratesLegacyBishopContextMd_WhenRootFileExists()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Migrate"));
            var legacyFile = Path.Combine(temp, "BISHOP_CONTEXT.md");
            File.WriteAllText(legacyFile, "old content");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            File.Exists(legacyFile).Should().BeFalse("legacy root file must be removed");
            File.Exists(Path.Combine(temp, ".bishop", "BISHOP_CONTEXT.md")).Should().BeTrue("file must be in .bishop/");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_MigratesLegacyBishopNotesMd_WhenRootFileExists()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Migrate"));
            var legacyNotes = Path.Combine(temp, "BISHOP_NOTES.md");
            File.WriteAllText(legacyNotes, "my notes");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            File.Exists(legacyNotes).Should().BeFalse("legacy root notes file must be removed");
            File.Exists(Path.Combine(temp, ".bishop", "BISHOP_NOTES.md")).Should().BeTrue("notes must be in .bishop/");
            File.ReadAllText(Path.Combine(temp, ".bishop", "BISHOP_NOTES.md")).Should().Be("my notes", "content must be preserved");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_Migration_IsIdempotent_WhenFilesAlreadyInSubfolder()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Idempotent"));
            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            await sut.SeedAsync(temp);

            File.Exists(Path.Combine(temp, ".bishop", "BISHOP_CONTEXT.md")).Should().BeTrue();
            File.Exists(Path.Combine(temp, "BISHOP_CONTEXT.md")).Should().BeFalse();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_LeavesLegacyContextFileInPlace_WhenNewFileAlreadyExists()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Conflict"));

            var legacyContext = Path.Combine(temp, WorkspaceContextSeeder.BishopContextFileName);
            File.WriteAllText(legacyContext, "legacy content");
            var bishopDir = Path.Combine(temp, WorkspaceContextSeeder.BishopFolder);
            Directory.CreateDirectory(bishopDir);
            File.WriteAllText(Path.Combine(bishopDir, WorkspaceContextSeeder.BishopContextFileName), "existing new content");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            File.Exists(legacyContext).Should().BeTrue("legacy root file must not be moved or deleted when new file already exists");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PropagatesException_WhenLegacyBishopContextMdIsLocked()
    {
        var temp = CreateTempDir();
        FileStream? lockStream = null;
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("MigrateLock"));
            var legacyFile = Path.Combine(temp, WorkspaceContextSeeder.BishopContextFileName);
            File.WriteAllText(legacyFile, "old content");
            lockStream = File.Open(legacyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            Func<Task> act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            lockStream?.Dispose();
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_PropagatesException_WhenLegacyBishopNotesMdIsLocked()
    {
        var temp = CreateTempDir();
        FileStream? lockStream = null;
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("MigrateNotesLock"));
            var legacyNotes = Path.Combine(temp, "BISHOP_NOTES.md");
            File.WriteAllText(legacyNotes, "my notes");
            lockStream = File.Open(legacyNotes, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            Func<Task> act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            lockStream?.Dispose();
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_Succeeds_WhenRetried_AfterLegacyFileLockReleased()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("MigrateRetry"));
            var legacyFile = Path.Combine(temp, WorkspaceContextSeeder.BishopContextFileName);
            File.WriteAllText(legacyFile, "old content");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);

            using (var lockStream = File.Open(legacyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Func<Task> locked = () => sut.SeedAsync(temp);
                await locked.Should().ThrowAsync<IOException>();
            }

            await sut.SeedAsync(temp);

            File.Exists(legacyFile).Should().BeFalse("legacy root file must be removed after successful retry");
            File.Exists(Path.Combine(temp, WorkspaceContextSeeder.BishopFolder, WorkspaceContextSeeder.BishopContextFileName))
                .Should().BeTrue("file must be in .bishop/ after successful retry");
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_RewritesLegacyPointerLine_InContextMd()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Rewrite"));
            var contextPath = Path.Combine(temp, "CONTEXT.md");
            File.WriteAllText(contextPath, $"# Project\r\n\r\n{WorkspaceContextSeeder.LegacyPointerLine}\r\n\r\nBody.\r\n");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(contextPath);
            updated.Should().Contain(WorkspaceContextSeeder.PointerLine);
            updated.Should().NotContain(WorkspaceContextSeeder.LegacyPointerLine);
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_RewritesLegacyPointerLine_InClaudeMd()
    {
        var temp = CreateTempDir();
        try
        {
            await SeedRegisteredWorkspaceAsync(temp, name: U("Rewrite"));
            var claudePath = Path.Combine(temp, "CLAUDE.md");
            File.WriteAllText(claudePath, $"# Project\r\n\r\n{WorkspaceContextSeeder.LegacyPointerLine}\r\n");

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            var updated = File.ReadAllText(claudePath);
            updated.Should().Contain(WorkspaceContextSeeder.PointerLine);
            updated.Should().NotContain(WorkspaceContextSeeder.LegacyPointerLine);
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled()
    {
        var temp = CreateTempDir();
        try
        {
            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = () => sut.SeedAsync(temp, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_ThrowsOperationCanceledException_WhenCancelledDuringQuery()
    {
        var temp = CreateTempDir();
        var dbName = Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        try
        {
            // Setup phase: no throwing interceptor so EnsureCreated + seed succeed.
            var setupOptions = new DbContextOptionsBuilder<BishopDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(new SqliteForeignKeyInterceptor())
                .EnableServiceProviderCaching(false)
                .Options;

            using var db = new BishopDbContext(setupOptions);
            db.Database.EnsureCreated();
            db.Workspaces.Add(new Workspace { Id = Guid.NewGuid(), Name = U("ws"), Path = temp, Position = 1 });
            await db.SaveChangesAsync();

            // Exercise phase: factory injects the throwing interceptor so the
            // ToListAsync() call inside ResolveWorkspaceAsync is cancelled mid-query.
            var factoryOptions = new DbContextOptionsBuilder<BishopDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(new SqliteForeignKeyInterceptor(), new WorkspacesQueryCancellingInterceptor())
                .EnableServiceProviderCaching(false)
                .Options;

            var sut = new WorkspaceContextSeeder(new CancellingInterceptorFactory(factoryOptions), TestBootstrappers.NoOp);

            var act = () => sut.SeedAsync(temp);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            CleanupTempDir(temp);
        }
    }

    [Fact]
    public async Task SeedAsync_ResolvesExactlyOneWorkspace_WhenDuplicateNormalizedPathsExist()
    {
        var temp = CreateTempDir();
        try
        {
            var firstName = U("First");
            var secondName = U("Second");
            var workspace1 = new Workspace { Id = Guid.NewGuid(), Name = firstName, Path = temp, Position = 1 };
            var workspace2 = new Workspace { Id = Guid.NewGuid(), Name = secondName, Path = temp, Position = 2 };
            _db.Workspaces.AddRange(workspace1, workspace2);
            await _db.SaveChangesAsync();

            var sut = new WorkspaceContextSeeder(_factory, TestBootstrappers.NoOp);
            await sut.SeedAsync(temp);

            // ResolveWorkspaceAsync uses FirstOrDefault with no ORDER BY — which workspace
            // wins is non-deterministic (depends on SQLite row order). Assert that exactly
            // one of the two is resolved, not both or neither.
            var bishopContent = File.ReadAllText(Path.Combine(temp, ".bishop", "BISHOP_CONTEXT.md"));
            var containsFirst = bishopContent.Contains($"# BISHOP_CONTEXT — {firstName}", StringComparison.Ordinal);
            var containsSecond = bishopContent.Contains($"# BISHOP_CONTEXT — {secondName}", StringComparison.Ordinal);
            (containsFirst ^ containsSecond).Should().BeTrue(
                "ResolveWorkspaceAsync must resolve exactly one workspace even when two share the same normalized path");
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

    private sealed class WorkspacesQueryCancellingInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("Workspaces", StringComparison.OrdinalIgnoreCase))
                throw new OperationCanceledException("Simulated mid-query cancellation.");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class CancellingInterceptorFactory(DbContextOptions<BishopDbContext> options)
        : IDbContextFactory<BishopDbContext>
    {
        public BishopDbContext CreateDbContext() => new(options);
    }
}

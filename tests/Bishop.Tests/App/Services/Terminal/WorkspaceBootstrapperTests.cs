using Bishop.App.Services.Terminal;
using FluentAssertions;

namespace Bishop.Tests.App.Services.Terminal;

public sealed class WorkspaceBootstrapperTests
{
    // ── EnsureGitIgnoreEntries ─────────────────────────────────────────────────

    [Fact]
    public void EnsureGitIgnoreEntries_ReturnsDefaultContent_WhenExistingIsNull()
    {
        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(null);

        result.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
        result.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
    }

    [Fact]
    public void EnsureGitIgnoreEntries_DoesNotModify_WhenEntriesAlreadyPresent()
    {
        var existing = $".env\n{WorkspaceBootstrapper.BishopIgnoreEntry}\n{WorkspaceBootstrapper.SlopwatchIgnoreEntry}\n";

        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(existing);

        result.Should().Be(existing);
    }

    [Fact]
    public void EnsureGitIgnoreEntries_AddsEntries_WhenMissing()
    {
        var existing = ".env\nnode_modules/\n";

        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(existing);

        result.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
        result.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
        result.Should().Contain(".env");
    }

    [Fact]
    public void EnsureGitIgnoreEntries_AddsMissingEntry_WhenOneAlreadyPresent()
    {
        var existing = $".env\n{WorkspaceBootstrapper.BishopIgnoreEntry}\n";

        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(existing);

        result.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
        result.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
    }

    [Fact]
    public void EnsureGitIgnoreEntries_RemovesLegacyGranularEntries()
    {
        var existing = ".env\n.bishop/runs/\n.bishop/denials.jsonl\nnode_modules/\n";

        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(existing);

        result.Should().NotContain(".bishop/runs/");
        result.Should().NotContain(".bishop/denials.jsonl");
        result.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
        result.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
        result.Should().Contain(".env");
        result.Should().Contain("node_modules/");
    }

    [Fact]
    public void EnsureGitIgnoreEntries_RemovesLegacyEntries_WhenBlanketEntryAlreadyPresent()
    {
        var existing = $".bishop/runs/\n.bishop/denials.jsonl\n{WorkspaceBootstrapper.BishopIgnoreEntry}\n{WorkspaceBootstrapper.SlopwatchIgnoreEntry}\n";

        var result = WorkspaceBootstrapper.EnsureGitIgnoreEntries(existing);

        result.Should().NotContain(".bishop/runs/");
        result.Should().NotContain(".bishop/denials.jsonl");
        result.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
        result.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
    }

    [Fact]
    public void EnsureGitIgnoreEntries_IsIdempotent()
    {
        var first = WorkspaceBootstrapper.EnsureGitIgnoreEntries(null);
        var second = WorkspaceBootstrapper.EnsureGitIgnoreEntries(first);

        second.Should().Be(first);
    }

    // ── EnsureBootstrappedAsync — file integration ────────────────────────────

    [Fact]
    public async Task EnsureBootstrappedAsync_CreatesGitIgnore_WhenMissing()
    {
        var dir = CreateTempDir();
        try
        {
            var sut = new WorkspaceBootstrapper();

            await sut.EnsureBootstrappedAsync(dir);

            var path = Path.Combine(dir, WorkspaceBootstrapper.GitIgnoreFileName);
            File.Exists(path).Should().BeTrue();
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
            content.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_RewritesGitIgnore_WhenLegacyEntriesPresent()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, WorkspaceBootstrapper.GitIgnoreFileName);
            await File.WriteAllTextAsync(path, ".env\n.bishop/runs/\n.bishop/denials.jsonl\n");
            var sut = new WorkspaceBootstrapper();

            await sut.EnsureBootstrappedAsync(dir);

            var content = await File.ReadAllTextAsync(path);
            content.Should().NotContain(".bishop/runs/");
            content.Should().NotContain(".bishop/denials.jsonl");
            content.Should().Contain(WorkspaceBootstrapper.BishopIgnoreEntry);
            content.Should().Contain(WorkspaceBootstrapper.SlopwatchIgnoreEntry);
            content.Should().Contain(".env");
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_IsIdempotent_OnAlreadyBootstrappedWorkspace()
    {
        var dir = CreateTempDir();
        try
        {
            var sut = new WorkspaceBootstrapper();
            await sut.EnsureBootstrappedAsync(dir);
            var gitIgnorePath = Path.Combine(dir, WorkspaceBootstrapper.GitIgnoreFileName);
            var firstPass = await File.ReadAllTextAsync(gitIgnorePath);

            await sut.EnsureBootstrappedAsync(dir);

            var secondPass = await File.ReadAllTextAsync(gitIgnorePath);
            secondPass.Should().Be(firstPass);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_SkipsGitInit_WhenDotGitDirectoryExists()
    {
        var dir = CreateTempDir();
        try
        {
            // Pre-create .git with a sentinel file so we can detect whether `git init`
            // would have wiped/recreated it.
            var dotGit = Path.Combine(dir, ".git");
            Directory.CreateDirectory(dotGit);
            var sentinel = Path.Combine(dotGit, "sentinel.txt");
            await File.WriteAllTextAsync(sentinel, "marker");
            var sut = new WorkspaceBootstrapper();

            await sut.EnsureBootstrappedAsync(dir);

            File.Exists(sentinel).Should().BeTrue("existing .git directory must be left untouched");
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_DoesNothing_WhenPathIsWhitespace()
    {
        var sut = new WorkspaceBootstrapper();

        await sut.EnsureBootstrappedAsync("   ");
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bishop-boot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupTempDir(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}

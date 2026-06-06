using System.Text.Json;
using System.Text.Json.Nodes;
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

    // ── IsDotNetWorkspace ─────────────────────────────────────────────────────

    [Fact]
    public void IsDotNetWorkspace_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), "bishop-missing-" + Guid.NewGuid().ToString("N"));

        WorkspaceBootstrapper.IsDotNetWorkspace(path).Should().BeFalse();
    }

    [Fact]
    public void IsDotNetWorkspace_ReturnsFalse_WhenNoSlnOrCsprojAtRoot()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "README.md"), "x");

            WorkspaceBootstrapper.IsDotNetWorkspace(dir).Should().BeFalse();
        }
        finally { CleanupTempDir(dir); }
    }

    [Fact]
    public void IsDotNetWorkspace_ReturnsTrue_WhenSlnPresent()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Foo.sln"), "");

            WorkspaceBootstrapper.IsDotNetWorkspace(dir).Should().BeTrue();
        }
        finally { CleanupTempDir(dir); }
    }

    [Fact]
    public void IsDotNetWorkspace_ReturnsTrue_WhenCsprojPresent()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Foo.csproj"), "");

            WorkspaceBootstrapper.IsDotNetWorkspace(dir).Should().BeTrue();
        }
        finally { CleanupTempDir(dir); }
    }

    [Fact]
    public void IsDotNetWorkspace_DoesNotRecurseIntoSubdirectories()
    {
        var dir = CreateTempDir();
        try
        {
            var sub = Path.Combine(dir, "nested");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "Foo.csproj"), "");

            WorkspaceBootstrapper.IsDotNetWorkspace(dir).Should().BeFalse();
        }
        finally { CleanupTempDir(dir); }
    }

    // ── IsSlopwatchInManifest ─────────────────────────────────────────────────

    [Fact]
    public void IsSlopwatchInManifest_ReturnsFalse_WhenNull()
    {
        WorkspaceBootstrapper.IsSlopwatchInManifest(null).Should().BeFalse();
    }

    [Fact]
    public void IsSlopwatchInManifest_ReturnsFalse_WhenEmpty()
    {
        WorkspaceBootstrapper.IsSlopwatchInManifest("").Should().BeFalse();
    }

    [Fact]
    public void IsSlopwatchInManifest_ReturnsFalse_WhenInvalidJson()
    {
        WorkspaceBootstrapper.IsSlopwatchInManifest("{ not json").Should().BeFalse();
    }

    [Fact]
    public void IsSlopwatchInManifest_ReturnsFalse_WhenToolsMissing()
    {
        WorkspaceBootstrapper.IsSlopwatchInManifest("""{"version":1}""").Should().BeFalse();
    }

    [Fact]
    public void IsSlopwatchInManifest_ReturnsFalse_WhenSlopwatchNotListed()
    {
        var json = """{"version":1,"isRoot":true,"tools":{"dotnet-stryker":{"version":"4.0.0","commands":["dotnet-stryker"]}}}""";

        WorkspaceBootstrapper.IsSlopwatchInManifest(json).Should().BeFalse();
    }

    [Fact]
    public void IsSlopwatchInManifest_ReturnsTrue_WhenSlopwatchListed()
    {
        var json = "{\"version\":1,\"isRoot\":true,\"tools\":{\""
            + WorkspaceBootstrapper.SlopwatchPackageId
            + "\":{\"version\":\"0.4.0\",\"commands\":[\"slopwatch\"]}}}";

        WorkspaceBootstrapper.IsSlopwatchInManifest(json).Should().BeTrue();
    }

    // ── MergeClaudeSettings ───────────────────────────────────────────────────

    private static readonly string[] SampleRequired = { "Bash(bishop:*)", "Read(.bishop/**)" };

    [Fact]
    public void MergeClaudeSettings_ReturnsFreshJson_WhenInputIsNull()
    {
        var result = WorkspaceBootstrapper.MergeClaudeSettings(null, SampleRequired);

        var allow = ReadAllow(result);
        allow.Should().BeEquivalentTo(SampleRequired);
    }

    [Fact]
    public void MergeClaudeSettings_ReturnsFreshJson_WhenInputIsEmpty()
    {
        var result = WorkspaceBootstrapper.MergeClaudeSettings("   ", SampleRequired);

        var allow = ReadAllow(result);
        allow.Should().BeEquivalentTo(SampleRequired);
    }

    [Fact]
    public void MergeClaudeSettings_AddsPermissionsObject_WhenMissing()
    {
        var existing = """{"theme":"dark"}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, SampleRequired);

        var root = JsonNode.Parse(result)!.AsObject();
        root["theme"]!.GetValue<string>().Should().Be("dark");
        ReadAllow(result).Should().BeEquivalentTo(SampleRequired);
    }

    [Fact]
    public void MergeClaudeSettings_AddsAllowArray_WhenMissing()
    {
        var existing = """{"permissions":{"deny":["Bash(rm:*)"]}}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, SampleRequired);

        var perms = JsonNode.Parse(result)!.AsObject()["permissions"]!.AsObject();
        perms["deny"]!.AsArray().Select(n => n!.GetValue<string>()).Should().ContainSingle().Which.Should().Be("Bash(rm:*)");
        ReadAllow(result).Should().BeEquivalentTo(SampleRequired);
    }

    [Fact]
    public void MergeClaudeSettings_AddsMissingEntries_WhenPartialOverlap()
    {
        var existing = """{"permissions":{"allow":["Bash(bishop:*)","WebFetch(*)"]}}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, SampleRequired);

        ReadAllow(result).Should().BeEquivalentTo("Bash(bishop:*)", "WebFetch(*)", "Read(.bishop/**)");
    }

    [Fact]
    public void MergeClaudeSettings_ReturnsExistingUnchanged_WhenFullOverlap()
    {
        var existing = """{"permissions":{"allow":["Bash(bishop:*)","Read(.bishop/**)","Extra(*)"]},"theme":"dark"}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, SampleRequired);

        result.Should().Be(existing);
    }

    [Fact]
    public void MergeClaudeSettings_StripsLegacyBishopGlobs()
    {
        var existing = """{"permissions":{"allow":["Bash(bishop:*)","Write(./.bishop/**)","Read(./.bishop/**)","Extra(*)"]}}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, WorkspaceBootstrapper.ClaudeAllowList);

        var allow = ReadAllow(result);
        allow.Should().NotContain("Write(./.bishop/**)");
        allow.Should().NotContain("Read(./.bishop/**)");
        allow.Should().Contain("Write(.bishop/**)");
        allow.Should().Contain("Read(.bishop/**)");
        allow.Should().Contain("Extra(*)");
    }

    [Fact]
    public void MergeClaudeSettings_PreservesUnrelatedTopLevelKeys()
    {
        var existing = """{"theme":"dark","model":"sonnet","permissions":{"deny":["Bash(rm:*)"]}}""";

        var result = WorkspaceBootstrapper.MergeClaudeSettings(existing, SampleRequired);

        var root = JsonNode.Parse(result)!.AsObject();
        root["theme"]!.GetValue<string>().Should().Be("dark");
        root["model"]!.GetValue<string>().Should().Be("sonnet");
        root["permissions"]!.AsObject()["deny"]!.AsArray()
            .Select(n => n!.GetValue<string>()).Should().ContainSingle().Which.Should().Be("Bash(rm:*)");
    }

    [Fact]
    public void MergeClaudeSettings_IsIdempotent()
    {
        var first = WorkspaceBootstrapper.MergeClaudeSettings(null, WorkspaceBootstrapper.ClaudeAllowList);
        var second = WorkspaceBootstrapper.MergeClaudeSettings(first, WorkspaceBootstrapper.ClaudeAllowList);

        second.Should().Be(first);
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_CreatesClaudeSettings_WhenMissing()
    {
        var dir = CreateTempDir();
        try
        {
            var sut = new WorkspaceBootstrapper();

            await sut.EnsureBootstrappedAsync(dir);

            var path = Path.Combine(dir, WorkspaceBootstrapper.ClaudeSettingsRelativePath);
            File.Exists(path).Should().BeTrue();
            var allow = ReadAllow(await File.ReadAllTextAsync(path));
            allow.Should().BeEquivalentTo(WorkspaceBootstrapper.ClaudeAllowList);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_MergesClaudeSettings_WhenPartiallyPresent()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, WorkspaceBootstrapper.ClaudeSettingsRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """{"theme":"dark","permissions":{"allow":["Bash(bishop:*)","CustomEntry(*)"]}}""");
            var sut = new WorkspaceBootstrapper();

            await sut.EnsureBootstrappedAsync(dir);

            var content = await File.ReadAllTextAsync(path);
            var root = JsonNode.Parse(content)!.AsObject();
            root["theme"]!.GetValue<string>().Should().Be("dark");
            var allow = ReadAllow(content);
            allow.Should().Contain("CustomEntry(*)");
            allow.Should().Contain(WorkspaceBootstrapper.ClaudeAllowList);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_DoesNotRewriteClaudeSettings_WhenAllEntriesPresent()
    {
        var dir = CreateTempDir();
        try
        {
            var sut = new WorkspaceBootstrapper();
            await sut.EnsureBootstrappedAsync(dir);
            var path = Path.Combine(dir, WorkspaceBootstrapper.ClaudeSettingsRelativePath);
            var firstContent = await File.ReadAllTextAsync(path);

            await sut.EnsureBootstrappedAsync(dir);

            (await File.ReadAllTextAsync(path)).Should().Be(firstContent);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    private static List<string> ReadAllow(string json)
        => JsonNode.Parse(json)!.AsObject()["permissions"]!.AsObject()["allow"]!.AsArray()
            .Select(n => n!.GetValue<string>()).ToList();

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

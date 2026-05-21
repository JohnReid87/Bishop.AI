using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class GetCardCommitTests : IDisposable
{
    private readonly string _tempDir;

    public GetCardCommitTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_tempDir, recursive: true);
    }

    private static void Git(string workingDir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit();
    }

    private void InitRepo()
    {
        Git(_tempDir, "init");
        Git(_tempDir, "config", "user.email", "test@example.com");
        Git(_tempDir, "config", "user.name", "Test");
    }

    private void AddCommit(string message)
    {
        File.WriteAllText(Path.Combine(_tempDir, $"{Guid.NewGuid()}.txt"), message);
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", message);
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsNotFound_WhenNoMatchingCommit()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Do something unrelated");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        result.Should().BeOfType<GetCardCommitResult.NotFound>();
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsNotAGitRepo_WhenDirectoryIsNotRepo()
    {
        // Arrange
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        result.Should().BeOfType<GetCardCommitResult.NotAGitRepo>();
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsFound_WhenCommitContainsCardRefWithHash()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Add lane CRUD (card #42)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        var found = result.Should().BeOfType<GetCardCommitResult.Found>().Subject;
        found.Commit.ShortHash.Should().NotBeNullOrEmpty();
        found.Commit.FullHash.Should().HaveLength(40);
        found.Commit.Timestamp.Should().NotBe(default);
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsFound_WhenCommitContainsCardRefWithoutHash()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Add lane CRUD (card 42)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        result.Should().BeOfType<GetCardCommitResult.Found>();
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsNotFound_WhenNoCommitsYet()
    {
        // Arrange
        InitRepo();
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        result.Should().BeOfType<GetCardCommitResult.NotFound>();
    }

    [Fact]
    public async Task GetCardCommitAsync_ReturnsMostRecentCommit_WhenMultipleMatchingCommitsExist()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: First attempt (card #42)");
        AddCommit("fix: Second attempt (card #42)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert — git log -1 returns the newest; verify it's the second commit
        var found = result.Should().BeOfType<GetCardCommitResult.Found>().Subject;

        // The most recent commit's hash should not equal the first commit's hash.
        // We verify this by checking the first commit is no longer what git log -1 returns.
        // Re-run with -2 to get both and confirm ordering.
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            WorkingDirectory = _tempDir,
        };
        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add("--perl-regexp");
        psi.ArgumentList.Add("--grep=\\(card #?42\\)");
        psi.ArgumentList.Add("-2");
        psi.ArgumentList.Add("--format=%H");
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        var hashes = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .ToList();

        hashes.Should().HaveCount(2);
        found.Commit.FullHash.Should().Be(hashes[0]); // newest is first in git log
    }

    [Fact]
    public async Task GetCardCommitAsync_IsPushedFalse_WhenNoUpstreamConfigured()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Add thing (card #42)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        var found = result.Should().BeOfType<GetCardCommitResult.Found>().Subject;
        found.Commit.IsPushed.Should().BeFalse();
    }

    [Fact]
    public async Task GetCardCommitAsync_IsPushedTrue_WhenCommitHasBeenPushedToUpstream()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Add thing (card #42)");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        Git(barePath, "init", "--bare");
        Git(_tempDir, "remote", "add", "origin", barePath);
        Git(_tempDir, "push", "-u", "origin", "HEAD");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        var found = result.Should().BeOfType<GetCardCommitResult.Found>().Subject;
        found.Commit.IsPushed.Should().BeTrue();
    }

    [Fact]
    public async Task GetCardCommitAsync_IsPushedFalse_WhenCommitIsAheadOfUpstream()
    {
        // Arrange
        InitRepo();
        AddCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        Git(barePath, "init", "--bare");
        Git(_tempDir, "remote", "add", "origin", barePath);
        Git(_tempDir, "push", "-u", "origin", "HEAD");

        AddCommit("feat: Add thing (card #42)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        var found = result.Should().BeOfType<GetCardCommitResult.Found>().Subject;
        found.Commit.IsPushed.Should().BeFalse();
    }

    [Fact]
    public async Task GetCardCommitAsync_DoesNotMatchDifferentCardNumber()
    {
        // Arrange
        InitRepo();
        AddCommit("feat: Add thing (card #420)");
        var sut = new GitCli();

        // Act
        var result = await sut.GetCardCommitAsync(42, _tempDir);

        // Assert
        result.Should().BeOfType<GetCardCommitResult.NotFound>();
    }
}

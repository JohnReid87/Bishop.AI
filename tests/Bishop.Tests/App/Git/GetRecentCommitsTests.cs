using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class GetRecentCommitsTests : IDisposable
{
    private readonly string _tempDir;

    public GetRecentCommitsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Git creates read-only files in .git/objects/ on Windows; clear the attribute before deleting.
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

    private void InitRepoWithCommit(string subject = "Initial commit")
    {
        Git(_tempDir, "init");
        Git(_tempDir, "config", "user.email", "test@example.com");
        Git(_tempDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", subject);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsSuccess_WithOneCommit()
    {
        // Arrange
        InitRepoWithCommit("Fix the thing");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.Commits.Should().HaveCount(1);
        success.Commits[0].Subject.Should().Be("Fix the thing");
        success.Commits[0].ShortHash.Should().NotBeNullOrEmpty();
        success.Commits[0].FullHash.Should().HaveLength(40);
        success.Commits[0].Timestamp.Should().NotBe(default);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNoCommits_WhenRepoIsEmpty()
    {
        // Arrange
        Git(_tempDir, "init");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        result.Should().BeOfType<GetRecentCommitsResult.NoCommits>();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNotAGitRepo_WhenDirectoryIsNotRepo()
    {
        // Arrange
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        result.Should().BeOfType<GetRecentCommitsResult.NotAGitRepo>();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsAtMostFiveCommits_WhenRepoHasMore()
    {
        // Arrange
        InitRepoWithCommit("Commit 1");
        for (var i = 2; i <= 7; i++)
        {
            File.WriteAllText(Path.Combine(_tempDir, $"file{i}.txt"), $"content {i}");
            Git(_tempDir, "add", ".");
            Git(_tempDir, "commit", "-m", $"Commit {i}");
        }
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.Commits.Should().HaveCount(5);
    }
}

using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class GetRecentCommitsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _gitPath = ResolveGitPath();

    public GetRecentCommitsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    private static string? ResolveGitPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var gitCandidate = Path.Combine(dir, "git.exe");
            if (File.Exists(gitCandidate))
                return gitCandidate;
        }
        return null;
    }

    public void Dispose()
    {
        // Git creates read-only files in .git/objects/ on Windows; clear the attribute before deleting.
        foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_tempDir, recursive: true);
    }

    private void Git(params string[] args) => GitInDir(_tempDir, args);

    private void GitInDir(string workingDir, params string[] args)
    {
        var gitExe = _gitPath ?? "git";
        var psi = new System.Diagnostics.ProcessStartInfo(gitExe)
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
        Git("init");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git("add", ".");
        Git("commit", "-m", subject);
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
        success.Commits[0].Body.Should().BeEmpty();
        success.Commits[0].ShortHash.Should().NotBeNullOrEmpty();
        success.Commits[0].FullHash.Should().HaveLength(40);
        success.Commits[0].Timestamp.Should().NotBe(default);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_PopulatesBody_WhenCommitHasMultiParagraphMessage()
    {
        // Arrange
        Git("init");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git("add", ".");
        Git("commit", "-m", "Add the thing\n\nThis explains why.\n\nCo-Authored-By: Test <t@t.com>");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.Commits[0].Subject.Should().Be("Add the thing");
        success.Commits[0].Body.Should().Be("This explains why.\n\nCo-Authored-By: Test <t@t.com>");
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNoCommits_WhenRepoIsEmpty()
    {
        // Arrange
        Git("init");
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
            Git("add", ".");
            Git("commit", "-m", $"Commit {i}");
        }
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.Commits.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_AllCommitsIsPushedTrue_WhenAllCommitsPushedToUpstream()
    {
        // Arrange
        InitRepoWithCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "-u", "origin", "HEAD");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().NotBeNullOrEmpty();
        success.Commits.Should().AllSatisfy(c => c.IsPushed.Should().BeTrue());
    }

    [Fact]
    public async Task GetRecentCommitsAsync_MarksOnlyPushedCommits_WhenSomeCommitsAheadOfUpstream()
    {
        // Arrange
        InitRepoWithCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "-u", "origin", "HEAD");

        File.WriteAllText(Path.Combine(_tempDir, "extra.txt"), "extra");
        Git("add", ".");
        Git("commit", "-m", "Unpushed commit");

        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().NotBeNullOrEmpty();
        success.Commits.Should().HaveCount(2);
        success.Commits[0].IsPushed.Should().BeFalse(); // most recent, not yet pushed
        success.Commits[1].IsPushed.Should().BeTrue();  // initial commit, pushed
    }

    [Fact]
    public async Task GetRecentCommitsAsync_AllCommitsIsPushedFalse_WhenNoUpstreamConfigured()
    {
        // Arrange
        InitRepoWithCommit("Initial commit");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().BeNull();
        success.Commits.Should().AllSatisfy(c => c.IsPushed.Should().BeFalse());
    }
}

using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class IsBranchMergedIntoTests : IDisposable
{
    private readonly string _root;
    private readonly string _repoDir;

    public IsBranchMergedIntoTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _repoDir = Path.Combine(_root, "repo");
        Directory.CreateDirectory(_repoDir);
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private static void Git(string workingDir, params string[] args) => TestGit.Run(workingDir, args);

    private void InitRepoWithCommit()
    {
        Git(_repoDir, "init");
        Git(_repoDir, "config", "user.email", "test@example.com");
        Git(_repoDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_repoDir, "file.txt"), "content");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Initial commit");
    }

    [Fact]
    public async Task ReturnsFalse_WhenBranchTipEqualsBaseTip()
    {
        // Arrange — branch created from HEAD but never had any commits added (unrun batch)
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "bishop/unrun-batch");
        Git(_repoDir, "checkout", "-");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "bishop/unrun-batch", "HEAD");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsFalse_WhenBranchTipUnchanged_AndBaseHasAdvanced()
    {
        // Arrange — unrun batch branch; base advances (e.g. another batch completes)
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "bishop/unrun-batch");
        Git(_repoDir, "checkout", "-");
        File.WriteAllText(Path.Combine(_repoDir, "other.txt"), "other");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Another commit on base");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "bishop/unrun-batch", "HEAD");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsFalse_WhenBranchNotMerged()
    {
        // Arrange
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "feature/x");
        File.WriteAllText(Path.Combine(_repoDir, "new.txt"), "extra");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Feature commit");
        Git(_repoDir, "checkout", "-");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "feature/x", "HEAD");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsTrue_WhenBranchMergedIntoBase()
    {
        // Arrange
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "feature/y");
        File.WriteAllText(Path.Combine(_repoDir, "new.txt"), "extra");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Feature commit");
        Git(_repoDir, "checkout", "-");
        Git(_repoDir, "merge", "--no-ff", "feature/y", "-m", "Merge feature/y");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "feature/y", "HEAD");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsFalse_WhenOnlySomeCommitsFromBranchEquivalentInBase()
    {
        // Arrange — branch has two commits; only one is cherry-picked into base so
        // git cherry produces a mix of "- sha" and "+ sha" lines. The real code uses
        // lines.All (requires every line to start with '-'); the mutant uses lines.Any
        // (true if at least one line starts with '-') and would return true incorrectly.
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "feature/partial");
        File.WriteAllText(Path.Combine(_repoDir, "a.txt"), "a");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Commit A");
        File.WriteAllText(Path.Combine(_repoDir, "b.txt"), "b");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Commit B");
        Git(_repoDir, "checkout", "-");
        // Cherry-pick only "Commit A" (the parent of the branch tip), leaving "Commit B" out.
        Git(_repoDir, "cherry-pick", "feature/partial^");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "feature/partial", "HEAD");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsTrue_WhenBranchSquashMergedIntoBase()
    {
        // Arrange
        InitRepoWithCommit();
        Git(_repoDir, "checkout", "-b", "feature/z");
        File.WriteAllText(Path.Combine(_repoDir, "new.txt"), "extra");
        Git(_repoDir, "add", ".");
        Git(_repoDir, "commit", "-m", "Feature commit");
        Git(_repoDir, "checkout", "-");
        Git(_repoDir, "merge", "--squash", "feature/z");
        Git(_repoDir, "commit", "-m", "Squash feature/z");
        var sut = new GitCli();

        // Act
        var result = await sut.IsBranchMergedIntoAsync(_repoDir, "feature/z", "HEAD");

        // Assert
        result.Should().BeTrue();
    }
}

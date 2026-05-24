using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class WorktreeTests : IDisposable
{
    private readonly string _root;
    private readonly string _repoDir;
    private readonly string _worktreeDir;

    public WorktreeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _repoDir = Path.Combine(_root, "repo");
        _worktreeDir = Path.Combine(_root, "repo-bishop-worktrees");
        Directory.CreateDirectory(_repoDir);
        Directory.CreateDirectory(_worktreeDir);
    }

    public void Dispose()
    {
        // Git creates read-only files in .git/objects/ on Windows; clear the attribute before deleting.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
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
    public async Task CreateWorktreeAsync_CreatesWorktreeAtPath_WithNewBranch()
    {
        // Arrange
        InitRepoWithCommit();
        var worktreePath = Path.Combine(_worktreeDir, "batch-1");
        var sut = new GitCli();

        // Act
        await sut.CreateWorktreeAsync(_repoDir, "batch/1", "HEAD", worktreePath);

        // Assert
        Directory.Exists(worktreePath).Should().BeTrue();
        File.Exists(Path.Combine(worktreePath, "file.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateWorktreeAsync_ChecksOutNewBranch_InWorktree()
    {
        // Arrange
        InitRepoWithCommit();
        var worktreePath = Path.Combine(_worktreeDir, "batch-2");
        var sut = new GitCli();

        // Act
        await sut.CreateWorktreeAsync(_repoDir, "batch/2", "HEAD", worktreePath);
        var branch = await sut.GetCurrentBranchAsync(worktreePath);

        // Assert
        branch.Should().Be("batch/2");
    }

    [Fact]
    public async Task GetCurrentBranchAsync_ReturnsMainBranch_ForMainRepo()
    {
        // Arrange
        InitRepoWithCommit();
        var sut = new GitCli();

        // Act
        var branch = await sut.GetCurrentBranchAsync(_repoDir);

        // Assert
        branch.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RemoveWorktreeAsync_Succeeds_ForCleanWorktree()
    {
        // Arrange
        InitRepoWithCommit();
        var worktreePath = Path.Combine(_worktreeDir, "batch-3");
        var sut = new GitCli();
        await sut.CreateWorktreeAsync(_repoDir, "batch/3", "HEAD", worktreePath);

        // Act
        await sut.RemoveWorktreeAsync(_repoDir, worktreePath);

        // Assert
        Directory.Exists(worktreePath).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveWorktreeAsync_Throws_ForDirtyWorktree()
    {
        // Arrange
        InitRepoWithCommit();
        var worktreePath = Path.Combine(_worktreeDir, "batch-4");
        var sut = new GitCli();
        await sut.CreateWorktreeAsync(_repoDir, "batch/4", "HEAD", worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "dirty.txt"), "uncommitted");

        // Act
        var act = () => sut.RemoveWorktreeAsync(_repoDir, worktreePath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git worktree remove*");
    }
}

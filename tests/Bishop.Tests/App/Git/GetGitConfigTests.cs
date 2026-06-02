using Bishop.App.Git;
using Bishop.App.Git.GetGitConfig;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class GetGitConfigTests : IDisposable
{
    private readonly string _tempDir;

    public GetGitConfigTests()
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

    private void Git(params string[] args) => GitInDir(_tempDir, args);
    private static void GitInDir(string workingDir, params string[] args) => TestGit.Run(workingDir, args);

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
    public async Task GetGitConfigAsync_ReturnsNotAGitRepo_WhenDirectoryIsNotRepo()
    {
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        result.Should().BeOfType<GetGitConfigResult.NotAGitRepo>();
    }

    [Fact]
    public async Task GetGitConfigAsync_OriginUrlIsNull_WhenNoRemote()
    {
        InitRepoWithCommit();
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.OriginUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetGitConfigAsync_OriginUrlPopulated_WhenRemoteSet()
    {
        InitRepoWithCommit();
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.OriginUrl.Should().Be(barePath);
    }

    [Fact]
    public async Task GetGitConfigAsync_UpstreamRefIsNull_WhenNoUpstream()
    {
        InitRepoWithCommit();
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.UpstreamRef.Should().BeNull();
        success.Ahead.Should().Be(0);
        success.Behind.Should().Be(0);
    }

    [Fact]
    public async Task GetGitConfigAsync_AheadCountReflectsUnpushedCommits_WhenUpstreamExists()
    {
        InitRepoWithCommit();
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "-u", "origin", "HEAD");
        for (var i = 1; i <= 2; i++)
        {
            File.WriteAllText(Path.Combine(_tempDir, $"extra{i}.txt"), $"c{i}");
            Git("add", ".");
            Git("commit", "-m", $"Extra {i}");
        }
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.UpstreamRef.Should().NotBeNull();
        success.UpstreamIsTracked.Should().BeTrue();
        success.Ahead.Should().Be(2);
        success.Behind.Should().Be(0);
    }

    [Fact]
    public async Task GetGitConfigAsync_StatusCountsZero_WhenWorkingTreeClean()
    {
        InitRepoWithCommit();
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.StagedCount.Should().Be(0);
        success.UnstagedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetGitConfigAsync_StatusCountsReflectStagedAndUnstaged_WhenDirty()
    {
        InitRepoWithCommit();
        // staged change: modify tracked file and stage it
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "changed");
        Git("add", "file.txt");
        // unstaged change: untracked new file
        File.WriteAllText(Path.Combine(_tempDir, "new.txt"), "new");
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.StagedCount.Should().Be(1);
        success.UnstagedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetGitConfigAsync_IdentityScopeIsRepo_WhenRepoLocalIdentitySet()
    {
        InitRepoWithCommit();
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.IdentityScope.Should().Be(GitIdentityScope.Repo);
        success.Name.Should().Be("Test");
        success.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetGitConfigAsync_IdentityScopeNotRepo_WhenRepoLocalIdentityUnset()
    {
        // Init without setting local user.name/user.email.
        Git("init");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.IdentityScope.Should().NotBe(GitIdentityScope.Repo);
    }

    [Fact]
    public async Task GetGitConfigAsync_BranchReflectsCurrentBranch()
    {
        InitRepoWithCommit();
        Git("checkout", "-b", "feature/x");
        var sut = new GitCli();

        var result = await sut.GetGitConfigAsync(_tempDir);

        var success = result.Should().BeOfType<GetGitConfigResult.Success>().Subject;
        success.Branch.Should().Be("feature/x");
    }
}

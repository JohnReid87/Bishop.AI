using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
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
    public async Task GetRecentCommitsAsync_ReturnsAtMostFiftyCommits_WhenRepoHasMore()
    {
        // Arrange
        InitRepoWithCommit("Commit 1");
        for (var i = 2; i <= 51; i++)
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
        success.Commits.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_UnpushedCountReflectsTrueCount_WhenUpstreamExists()
    {
        // Arrange
        InitRepoWithCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "-u", "origin", "HEAD");
        for (var i = 1; i <= 3; i++)
        {
            File.WriteAllText(Path.Combine(_tempDir, $"unpushed{i}.txt"), $"content {i}");
            Git("add", ".");
            Git("commit", "-m", $"Unpushed commit {i}");
        }
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UnpushedCount.Should().Be(3);
        success.Commits.Count(c => !c.IsPushed).Should().Be(3);
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
        success.UpstreamIsTracked.Should().BeTrue();
        success.Commits.Should().AllSatisfy(c => c.IsPushed.Should().BeTrue());
    }

    [Fact]
    public async Task GetRecentCommitsAsync_UsesFallbackUpstream_WhenBranchPushedWithoutTracking()
    {
        // Arrange — push without -u so origin/<branch> exists but local tracking is unset.
        InitRepoWithCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "origin", "HEAD");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().StartWith("origin/");
        success.UpstreamIsTracked.Should().BeFalse();
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
    public async Task GetRecentCommitsAsync_AllCommitsIsPushedFalse_WhenNoRemoteBranch()
    {
        // Arrange — no remote configured at all.
        InitRepoWithCommit("Initial commit");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().BeNull();
        success.UpstreamIsTracked.Should().BeFalse();
        success.Commits.Should().AllSatisfy(c => c.IsPushed.Should().BeFalse());
        // Mutant kill: unpushedCount = finalCommits.Count(c => !c.IsPushed) when upstream is null.
        // A mutation of !c.IsPushed → c.IsPushed would produce 0 instead of 1.
        success.UnpushedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ThrowsInvalidOperationException_WhenGitExits128WithUnrecognizedError()
    {
        // Arrange — init then corrupt HEAD to point at a non-existent object so git
        // exits 128 with a message containing neither "not a git repository" nor
        // "does not have any commits yet". The real code throws; a mutation that
        // changes the detection string to "" returns NoCommits instead of throwing.
        Git("init");
        File.WriteAllText(Path.Combine(_tempDir, ".git", "HEAD"), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n");
        var sut = new GitCli();

        // Act
        var act = () => sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ParsesEmptySubject_WhenCommitMessageStartsWithNewline()
    {
        // Arrange — commit whose stored message begins with \n so that IndexOf('\n')
        // returns 0. The real code (newlineIdx < 0) goes to the else branch and
        // produces an empty subject; the mutant (newlineIdx <= 0) wrongly takes the
        // if branch and returns the full trimmed message as the subject.
        Git("init");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git("add", ".");
        var msgFile = Path.Combine(_tempDir, "msg.txt");
        File.WriteAllText(msgFile, "\nBody only, no subject");
        Git("commit", "--cleanup=verbatim", "-F", msgFile);
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.Commits.Should().HaveCount(1);
        success.Commits[0].Subject.Should().Be("");
        success.Commits[0].Body.Should().Be("Body only, no subject");
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNullUpstreamRef_WhenHeadIsDetached()
    {
        // Arrange — detached HEAD; git rev-parse --abbrev-ref HEAD returns the literal
        // string "HEAD". The real guard (|| check) returns null upstream immediately.
        // The mutant (&& check) skips the guard and proceeds to the fallback lookup for
        // refs/remotes/origin/HEAD, which exists here as a regular tracking ref, so
        // the mutant would return a non-null upstream ref.
        InitRepoWithCommit("Initial commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "origin", "HEAD");
        // Establish refs/remotes/origin/HEAD as a regular tracking ref so the mutant
        // would discover it via for-each-ref during the fallback path.
        Git("update-ref", "refs/remotes/origin/HEAD", "HEAD");
        Git("checkout", "--detach", "HEAD");
        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNonNullUpstreamRef_AfterSquashRebaseAndForcePush()
    {
        // Arrange — push two commits, squash them via reset+commit, then force-push.
        // This simulates the scenario where @{u} context resolution fails in a non-interactive
        // process but git for-each-ref still resolves the upstream from branch config.
        InitRepoWithCommit("First commit");
        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        GitInDir(barePath, "init", "--bare");
        Git("remote", "add", "origin", barePath);
        Git("push", "-u", "origin", "HEAD");

        File.WriteAllText(Path.Combine(_tempDir, "second.txt"), "second");
        Git("add", ".");
        Git("commit", "-m", "Second commit");
        Git("push");

        Git("reset", "--soft", "HEAD~2");
        Git("commit", "-m", "Squashed commit");
        Git("push", "--force");

        var sut = new GitCli();

        // Act
        var result = await sut.GetRecentCommitsAsync(_tempDir);

        // Assert
        var success = result.Should().BeOfType<GetRecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().NotBeNull();
        success.Commits.Should().AllSatisfy(c => c.IsPushed.Should().BeTrue());
    }
}

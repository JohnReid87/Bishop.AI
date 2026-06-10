using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public class CommitsFlyoutViewModelTests
{
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly CommitsFlyoutViewModel _vm;

    private static readonly CommitInfo SampleCommit = new(
        ShortHash: "abc1234",
        FullHash: "abc1234567890",
        Subject: "feat: sample commit",
        Body: string.Empty,
        Timestamp: DateTimeOffset.UtcNow.AddHours(-1),
        IsPushed: true);

    public CommitsFlyoutViewModelTests()
    {
        _vm = new CommitsFlyoutViewModel(_mediator, TimeProvider.System);
    }

    private void SetupCommitsResult(GetRecentCommitsResult result)
        => _mediator
            .Send(Arg.Any<GetRecentCommitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

    private void SetupPushResult(PushResult result)
        => _mediator
            .Send(Arg.Any<PushCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

    [Fact]
    public async Task LoadAsync_Success_PopulatesCommits()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main"));

        await _vm.LoadAsync(@"C:\repo");

        _vm.Commits.Should().HaveCount(1);
        _vm.Commits[0].ShortHash.Should().Be("abc1234");
        _vm.Commits[0].Subject.Should().Be("feat: sample commit");
        _vm.HasCommits.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_Success_SetsShowSeparatorFalseOnLastItem()
    {
        var commits = new[]
        {
            SampleCommit,
            SampleCommit with { ShortHash = "def5678", FullHash = "def5678901234" },
        };
        SetupCommitsResult(new GetRecentCommitsResult.Success(commits, UpstreamRef: "origin/main"));

        await _vm.LoadAsync(@"C:\repo");

        _vm.Commits.Should().HaveCount(2);
        _vm.Commits[0].ShowSeparator.Should().BeTrue();
        _vm.Commits[1].ShowSeparator.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_NotAGitRepo_SetsStatusMessage()
    {
        SetupCommitsResult(new GetRecentCommitsResult.NotAGitRepo());

        await _vm.LoadAsync(@"C:\repo");

        _vm.HasCommits.Should().BeFalse();
        _vm.StatusMessage.Should().Be("Not a git repository");
        _vm.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_GitNotFound_SetsStatusMessage()
    {
        SetupCommitsResult(new GetRecentCommitsResult.GitNotFound());

        await _vm.LoadAsync(@"C:\repo");

        _vm.HasCommits.Should().BeFalse();
        _vm.StatusMessage.Should().Be("Git not installed or not on PATH");
    }

    [Fact]
    public async Task LoadAsync_NoCommits_SetsStatusMessage()
    {
        SetupCommitsResult(new GetRecentCommitsResult.NoCommits());

        await _vm.LoadAsync(@"C:\repo");

        _vm.HasCommits.Should().BeFalse();
        _vm.StatusMessage.Should().Be("No commits yet");
    }

    [Fact]
    public async Task LoadAsync_NoUpstream_PushDisabledWithPublishLabel()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: null));

        await _vm.LoadAsync(@"C:\repo");

        _vm.IsPushEnabled.Should().BeFalse();
        _vm.PushLabel.Should().Be("No remote branch — push with -u to publish");
    }

    [Fact]
    public async Task LoadAsync_UpToDate_PushDisabledWithUpToDateLabel()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UnpushedCount: 0));

        await _vm.LoadAsync(@"C:\repo");

        _vm.IsPushEnabled.Should().BeFalse();
        _vm.PushLabel.Should().Be("Up to date");
    }

    [Fact]
    public async Task LoadAsync_OneUnpushedTracked_ShowsSingularLabel()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: true, UnpushedCount: 1));

        await _vm.LoadAsync(@"C:\repo");

        _vm.IsPushEnabled.Should().BeTrue();
        _vm.PushLabel.Should().Be("Push 1 commit");
    }

    [Fact]
    public async Task LoadAsync_MultipleUnpushedTracked_ShowsPluralLabel()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: true, UnpushedCount: 3));

        await _vm.LoadAsync(@"C:\repo");

        _vm.PushLabel.Should().Be("Push 3 commits");
    }

    [Fact]
    public async Task LoadAsync_UnpushedNotTracked_ShowsSetUpstreamLabel()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: false, UnpushedCount: 2));

        await _vm.LoadAsync(@"C:\repo");

        _vm.PushLabel.Should().Be("Push 2 commits (will set upstream)");
        _vm.IsPushEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_Success_RefreshesCommits()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: true, UnpushedCount: 1));
        await _vm.LoadAsync(@"C:\repo");

        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UnpushedCount: 0));
        SetupPushResult(new PushResult(Success: true, Message: null));

        await _vm.PushCommand.ExecuteAsync(null);

        _vm.PushLabel.Should().Be("Up to date");
        _vm.IsPushEnabled.Should().BeFalse();
        _vm.PushError.Should().BeNull();
    }

    [Fact]
    public async Task PushAsync_Failure_SetsPushError()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: true, UnpushedCount: 1));
        await _vm.LoadAsync(@"C:\repo");

        SetupPushResult(new PushResult(Success: false, Message: "rejected"));

        await _vm.PushCommand.ExecuteAsync(null);

        _vm.PushError.Should().Be("Push failed: rejected");
        _vm.HasPushError.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_FailureWithNoMessage_SetsGenericError()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main", UpstreamIsTracked: true, UnpushedCount: 1));
        await _vm.LoadAsync(@"C:\repo");

        SetupPushResult(new PushResult(Success: false, Message: null));

        await _vm.PushCommand.ExecuteAsync(null);

        _vm.PushError.Should().Be("Push failed");
    }

    [Fact]
    public async Task RaiseCommitActivated_FiresEvent()
    {
        SetupCommitsResult(new GetRecentCommitsResult.Success([SampleCommit], UpstreamRef: "origin/main"));
        await _vm.LoadAsync(@"C:\repo");

        CommitRowViewModel? activated = null;
        _vm.CommitActivated += row => activated = row;

        _vm.RaiseCommitActivated(_vm.Commits[0]);

        activated.Should().BeSameAs(_vm.Commits[0]);
    }
}

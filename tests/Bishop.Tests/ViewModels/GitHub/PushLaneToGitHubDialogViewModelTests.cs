using System.ComponentModel;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.GitHub;

public class PushLaneToGitHubDialogViewModelTests
{
    private static PushLaneToGitHubDialogViewModel Make(IReadOnlyList<CardViewModel> cards)
        => new(cards, Guid.Empty, string.Empty, null!);

    [Fact]
    public void Constructor_EmptyInput_NothingToPush()
    {
        var vm = Make([]);

        vm.WillPushItems.Should().BeEmpty();
        vm.SkippedCount.Should().Be(0);
        vm.HasWillPush.Should().BeFalse();
        vm.PreviewSummary.Should().Be("0 to push, 0 already linked");
    }

    [Fact]
    public void Constructor_AllAlreadyLinked_AllSkipped()
    {
        var cards = new[]
        {
            new CardViewModel { Number = 1, Title = "First", GitHubIssueNumber = 101 },
            new CardViewModel { Number = 2, Title = "Second", GitHubIssueNumber = 102 },
        };

        var vm = Make(cards);

        vm.WillPushItems.Should().BeEmpty();
        vm.SkippedCount.Should().Be(2);
        vm.HasWillPush.Should().BeFalse();
        vm.PreviewSummary.Should().Be("0 to push, 2 already linked");
    }

    [Fact]
    public void Constructor_MixedWillPushAndSkipped_PartitionsCorrectly()
    {
        var cards = new[]
        {
            new CardViewModel { Number = 10, Title = "Unlinked one", GitHubIssueNumber = null },
            new CardViewModel { Number = 11, Title = "Already linked", GitHubIssueNumber = 99 },
            new CardViewModel { Number = 12, Title = "Unlinked two", GitHubIssueNumber = null },
        };

        var vm = Make(cards);

        vm.WillPushItems.Should().Equal("#10 Unlinked one", "#12 Unlinked two");
        vm.SkippedCount.Should().Be(1);
        vm.HasWillPush.Should().BeTrue();
        vm.PreviewSummary.Should().Be("2 to push, 1 already linked");
    }

    [Fact]
    public void ApplyResult_WithFailures_IncludesFailedSegment()
    {
        var vm = Make([]);

        vm.ApplyResult(pushed: 3, skipped: 1, failed: 2);

        vm.ResultSummary.Should().Be("3 pushed, 1 skipped, 2 failed");
        vm.HasResults.Should().BeTrue();
        vm.WasPushed.Should().BeTrue();
    }

    [Fact]
    public void ApplyResult_WithoutFailures_OmitsFailedSegment()
    {
        var vm = Make([]);

        vm.ApplyResult(pushed: 2, skipped: 1, failed: 0);

        vm.ResultSummary.Should().Be("2 pushed, 1 skipped");
        vm.HasResults.Should().BeTrue();
        vm.WasPushed.Should().BeTrue();
    }

    [Fact]
    public void ApplyResult_ZeroPushed_WasPushedFalse()
    {
        var vm = Make([]);

        vm.ApplyResult(pushed: 0, skipped: 0, failed: 1);

        vm.ResultSummary.Should().Be("0 pushed, 0 skipped, 1 failed");
        vm.HasResults.Should().BeTrue();
        vm.WasPushed.Should().BeFalse();
    }

    [Fact]
    public void ApplyError_FormatsMessageAndFlagsResults()
    {
        var vm = Make([]);

        vm.ApplyError("network down");

        vm.ResultSummary.Should().Be("Push failed: network down");
        vm.HasResults.Should().BeTrue();
        vm.WasPushed.Should().BeFalse();
    }

    [Fact]
    public void IsBusy_TogglingNotifiesIsIdle()
    {
        var vm = Make([]);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsBusy = true;

        vm.IsIdle.Should().BeFalse();
        changed.Should().Contain(nameof(PushLaneToGitHubDialogViewModel.IsIdle));
    }
}

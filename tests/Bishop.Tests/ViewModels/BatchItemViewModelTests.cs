using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class BatchItemViewModelTests
{
    [Fact]
    public void CanRun_TrueWhenOpen()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Open };

        vm.CanRun.Should().BeTrue();
        vm.CanFinish.Should().BeFalse();
        vm.CanAbandon.Should().BeFalse();
    }

    [Fact]
    public void CanFinish_TrueWhenWorkingWithoutPr()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, GitHubPrUrl = null };

        vm.CanRun.Should().BeFalse();
        vm.CanFinish.Should().BeTrue();
        vm.CanComplete.Should().BeFalse();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void CanComplete_TrueWhenWorkingWithPr()
    {
        var vm = new BatchItemViewModel
        {
            Status = BatchStatus.Working,
            GitHubPrUrl = "https://github.com/owner/repo/pull/1"
        };

        vm.CanFinish.Should().BeFalse();
        vm.CanComplete.Should().BeTrue();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void CanFinish_And_CanComplete_BothFalseWhenClosed()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Closed };

        vm.CanFinish.Should().BeFalse();
        vm.CanComplete.Should().BeFalse();
    }

    [Fact]
    public void StatusLabel_ReturnsOpenForOpenStatus()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Open };

        vm.StatusLabel.Should().Be("Open");
    }

    [Fact]
    public void StatusLabel_ReturnsWorkingForWorkingStatus()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working };

        vm.StatusLabel.Should().Be("Working");
    }

    [Fact]
    public void CardCountLabel_SingularForOneCard()
    {
        var vm = new BatchItemViewModel { CardCount = 1 };

        vm.CardCountLabel.Should().Be("1 card");
    }

    [Fact]
    public void CardCountLabel_PluralForMultipleCards()
    {
        var vm = new BatchItemViewModel { CardCount = 3 };

        vm.CardCountLabel.Should().Be("3 cards");
    }

    [Fact]
    public void CardCountLabel_PluralForZeroCards()
    {
        var vm = new BatchItemViewModel { CardCount = 0 };

        vm.CardCountLabel.Should().Be("0 cards");
    }

    [Fact]
    public void StatusLabel_ReturnsClosedForClosedStatus()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Closed };

        vm.StatusLabel.Should().Be("Closed");
    }

    [Fact]
    public void HasGitHubPr_TrueWhenUrlSet()
    {
        var vm = new BatchItemViewModel { GitHubPrUrl = "https://github.com/owner/repo/pull/1" };

        vm.HasGitHubPr.Should().BeTrue();
    }

    [Fact]
    public void HasGitHubPr_FalseWhenUrlNull()
    {
        var vm = new BatchItemViewModel { GitHubPrUrl = null };

        vm.HasGitHubPr.Should().BeFalse();
    }

    [Fact]
    public void HasGitHubPr_FalseWhenUrlEmpty()
    {
        var vm = new BatchItemViewModel { GitHubPrUrl = string.Empty };

        vm.HasGitHubPr.Should().BeFalse();
    }
}

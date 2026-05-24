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
    public void CanFinish_TrueWhenWorking()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working };

        vm.CanRun.Should().BeFalse();
        vm.CanFinish.Should().BeTrue();
        vm.CanAbandon.Should().BeTrue();
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
}

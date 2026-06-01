using Bishop.Core;
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

namespace Bishop.Tests.ViewModels.Batches;

public class BatchItemViewModelTests
{
    [Fact]
    public void OpenBatch_ShowsRunAndAbandon()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Open };

        vm.CanRun.Should().BeTrue();
        vm.CanResume.Should().BeFalse();
        vm.CanPause.Should().BeFalse();
        vm.CanCleanUp.Should().BeFalse();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void WorkingBatchWithNoStoppedAt_ShowsPauseOnly()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, FinishedAt = null, StoppedAt = null };

        vm.CanRun.Should().BeFalse();
        vm.CanPause.Should().BeTrue();
        vm.CanResume.Should().BeFalse();
        vm.CanCleanUp.Should().BeFalse();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void WorkingBatchWithStoppedAt_ShowsResumeOnly()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, FinishedAt = null, StoppedAt = DateTimeOffset.UtcNow };

        vm.CanRun.Should().BeFalse();
        vm.CanPause.Should().BeFalse();
        vm.CanResume.Should().BeTrue();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void WorkingBatchWithFinishedAt_ShowsMergeAbandon()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, FinishedAt = DateTimeOffset.UtcNow };

        vm.CanRun.Should().BeFalse();
        vm.CanResume.Should().BeFalse();
        vm.CanPause.Should().BeFalse();
        vm.CanMerge.Should().BeTrue();
        vm.CanAbandon.Should().BeTrue();
    }

    [Fact]
    public void ClosedBatch_ShowsNoButtons()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Closed };

        vm.CanRun.Should().BeFalse();
        vm.CanResume.Should().BeFalse();
        vm.CanPause.Should().BeFalse();
        vm.CanCleanUp.Should().BeFalse();
        vm.CanAbandon.Should().BeFalse();
    }

    [Fact]
    public void MergedBatchWithBranchOrWorktree_ShowsCleanUp()
    {
        var vm = new BatchItemViewModel { IsMerged = true, BranchExists = true };

        vm.CanCleanUp.Should().BeTrue();
    }

    [Fact]
    public void MergedBatchWithNoBranchAndNoWorktree_HidesCleanUp()
    {
        var vm = new BatchItemViewModel { IsMerged = true, BranchExists = false, WorktreeExists = false };

        vm.CanCleanUp.Should().BeFalse();
    }

    [Fact]
    public void UnmergedBatchWithBranch_HidesCleanUp()
    {
        var vm = new BatchItemViewModel { IsMerged = false, BranchExists = true };

        vm.CanCleanUp.Should().BeFalse();
    }

    [Fact]
    public void StatusLabel_ReturnsOpenForOpenStatus()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Open };

        vm.StatusLabel.Should().Be("Open");
    }

    [Fact]
    public void StatusLabel_ReturnsWorkingWhenWorkingAndFinishedAtNull()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, FinishedAt = null };

        vm.StatusLabel.Should().Be("Working");
    }

    [Fact]
    public void StatusLabel_ReturnsReadyWhenWorkingAndFinishedAtSet()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, FinishedAt = DateTimeOffset.UtcNow };

        vm.StatusLabel.Should().Be("Ready");
    }

    [Fact]
    public void StatusLabel_ReturnsMergedWhenWorkingAndIsMerged()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, IsMerged = true };

        vm.StatusLabel.Should().Be("Merged");
    }

    [Fact]
    public void StatusLabel_ReturnsMergedWhenWorkingAndIsMergedEvenWithFinishedAt()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working, IsMerged = true, FinishedAt = DateTimeOffset.UtcNow };

        vm.StatusLabel.Should().Be("Merged");
    }

    [Fact]
    public void StatusLabel_ReturnsClosedForClosedStatus()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Closed };

        vm.StatusLabel.Should().Be("Closed");
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
    public void IsStripEmpty_TrueWhenNoCards()
    {
        var vm = new BatchItemViewModel();

        vm.IsStripEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsStripEmpty_FalseWhenCardsPresent()
    {
        var vm = new BatchItemViewModel();
        vm.Cards.Add(new CardViewModel { Number = 1, Title = "t" });

        vm.IsStripEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsStripExpanded_TrueByDefault()
    {
        var vm = new BatchItemViewModel();

        vm.IsStripExpanded.Should().BeTrue();
    }

    [Fact]
    public void StripOpacity_ReducedForClosedBatch()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Closed };

        vm.StripOpacity.Should().Be(0.55);
    }

    [Fact]
    public void StripOpacity_FullForOpenBatch()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Open };

        vm.StripOpacity.Should().Be(1.0);
    }

    [Fact]
    public void StripOpacity_FullForWorkingBatch()
    {
        var vm = new BatchItemViewModel { Status = BatchStatus.Working };

        vm.StripOpacity.Should().Be(1.0);
    }

    [Fact]
    public void IsMergedColor_ActiveColorWhenMerged()
    {
        var merged = new BatchItemViewModel { IsMerged = true };
        var unmerged = new BatchItemViewModel { IsMerged = false };

        merged.IsMergedColor.Should().Be("#7fa87a");
        unmerged.IsMergedColor.Should().Be("#404040");
    }

    [Fact]
    public void BranchExistsColor_ActiveColorWhenBranchExists()
    {
        var withBranch = new BatchItemViewModel { BranchExists = true };
        var withoutBranch = new BatchItemViewModel { BranchExists = false };

        withBranch.BranchExistsColor.Should().Be("#7fa87a");
        withoutBranch.BranchExistsColor.Should().Be("#404040");
    }

    [Fact]
    public void WorktreeExistsColor_ActiveColorWhenWorktreeExists()
    {
        var withWorktree = new BatchItemViewModel { WorktreeExists = true };
        var withoutWorktree = new BatchItemViewModel { WorktreeExists = false };

        withWorktree.WorktreeExistsColor.Should().Be("#7fa87a");
        withoutWorktree.WorktreeExistsColor.Should().Be("#404040");
    }

    [Fact]
    public void CanRemove_TrueWhenClosed()
    {
        var closed = new BatchItemViewModel { Status = BatchStatus.Closed };
        var open = new BatchItemViewModel { Status = BatchStatus.Open };

        closed.CanRemove.Should().BeTrue();
        open.CanRemove.Should().BeFalse();
    }

    [Fact]
    public void Name_CanBeSetAndRaisesPropertyChanged()
    {
        var vm = new BatchItemViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Name = "my-batch";

        vm.Name.Should().Be("my-batch");
        changed.Should().Contain(nameof(BatchItemViewModel.Name));
    }

    [Fact]
    public void IsNameEditing_CanBeToggled()
    {
        var vm = new BatchItemViewModel();

        vm.IsNameEditing = true;

        vm.IsNameEditing.Should().BeTrue();
    }
}

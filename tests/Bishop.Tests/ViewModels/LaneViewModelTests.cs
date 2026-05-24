using Bishop.App.Cards.AddCard;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class LaneViewModelTests
{
    [Theory]
    [InlineData("To Do", true, false, false)]
    [InlineData("Backlog", false, true, false)]
    [InlineData("Done", false, false, true)]
    [InlineData("Doing", false, false, false)]
    public void LaneFlags_MatchName(string name, bool isToDo, bool isBacklog, bool isDone)
    {
        var vm = NewVm(name: name);

        vm.IsToDoLane.Should().Be(isToDo);
        vm.IsBacklogLane.Should().Be(isBacklog);
        vm.IsDoneLane.Should().Be(isDone);
    }

    [Fact]
    public void DisplayName_IncludesNameAndTotalCardCount()
    {
        var vm = NewVm(name: "To Do");
        vm.DisplayName.Should().Be("To Do (0)");

        vm.Cards.Add(new CardViewModel { Title = "A", LaneName = "To Do" });

        vm.DisplayName.Should().Be("To Do (1)");
    }

    [Fact]
    public void DisplayName_DoesNotChangeWhenFilterIsApplied()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "Alpha", LaneName = "To Do" });
        vm.Cards.Add(new CardViewModel { Title = "Beta", LaneName = "To Do" });

        vm.ApplyFilter("alpha");

        vm.FilteredCards.Should().HaveCount(1);
        vm.DisplayName.Should().Be("To Do (2)");
    }

    [Fact]
    public void CanWorkNext_TrueForToDoLaneWithCards()
    {
        var vm = NewVm(name: "To Do");
        vm.CanWorkNext.Should().BeFalse();

        vm.Cards.Add(new CardViewModel { Title = "A" });

        vm.CanWorkNext.Should().BeTrue();
    }

    [Fact]
    public void WorkNextTooltip_ChangesWithCanWorkNext()
    {
        var vm = NewVm(name: "To Do");
        vm.WorkNextTooltip.Should().Be("No cards");

        vm.Cards.Add(new CardViewModel { Title = "A" });

        vm.WorkNextTooltip.Should().Be("Loop it");
    }

    [Fact]
    public void IsImportVisible_TrueForBacklogWithGitHubRepo()
    {
        var vm = NewVm(name: "Backlog");
        vm.IsImportVisible.Should().BeFalse();

        vm.HasGitHubRepo = true;

        vm.IsImportVisible.Should().BeTrue();
    }

    [Fact]
    public void IsPushToGitHubVisible_TrueForDoneLaneWithGitHubRepo()
    {
        var vm = NewVm(name: "Done");
        vm.IsPushToGitHubVisible.Should().BeFalse();

        vm.HasGitHubRepo = true;

        vm.IsPushToGitHubVisible.Should().BeTrue();
    }

    [Fact]
    public void IsPlayVisible_TrueForToDoWhenNotRunning()
    {
        var vm = NewVm(name: "To Do");
        vm.IsPlayVisible.Should().BeTrue();
        vm.IsStopVisible.Should().BeFalse();

        vm.IsWorkNextRunning = true;

        vm.IsPlayVisible.Should().BeFalse();
        vm.IsStopVisible.Should().BeTrue();
    }

    [Fact]
    public void CanPlayWorkNext_FalseWhileRunning()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.CanPlayWorkNext.Should().BeTrue();

        vm.IsWorkNextRunning = true;

        vm.CanPlayWorkNext.Should().BeFalse();
    }

    [Fact]
    public void CanStopWorkNext_TrueOnlyWhileRunningAndNotStopping()
    {
        var vm = NewVm();
        vm.IsWorkNextRunning = true;
        vm.CanStopWorkNext.Should().BeTrue();

        vm.IsWorkNextStopping = true;

        vm.CanStopWorkNext.Should().BeFalse();
    }

    [Fact]
    public void StopWorkNextTooltip_ChangesWhileStopping()
    {
        var vm = NewVm();
        vm.StopWorkNextTooltip.Should().Be("Stop looping");

        vm.IsWorkNextStopping = true;

        vm.StopWorkNextTooltip.Should().Be("Stopping loop…");
    }

    [Fact]
    public void HasAddCardError_TrueWhenMessageIsSet()
    {
        var vm = NewVm();
        vm.HasAddCardError.Should().BeFalse();

        vm.AddCardErrorMessage = "Something went wrong";
        vm.HasAddCardError.Should().BeTrue();

        vm.AddCardErrorMessage = null;
        vm.HasAddCardError.Should().BeFalse();
    }

    [Fact]
    public void BeginAddCardCommand_SetsIsAddingCardAndClearsError()
    {
        var vm = NewVm();
        vm.IsAddingCard.Should().BeFalse();

        vm.BeginAddCardCommand.Execute(null);

        vm.IsAddingCard.Should().BeTrue();
        vm.AddCardErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CancelAddCardCommand_ResetsAddingState()
    {
        var vm = NewVm();
        vm.IsAddingCard = true;
        vm.NewCardTitle = "Draft";
        vm.AddCardErrorMessage = "Error";

        vm.CancelAddCardCommand.Execute(null);

        vm.IsAddingCard.Should().BeFalse();
        vm.NewCardTitle.Should().BeEmpty();
        vm.AddCardErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ApplyFilter_FiltersCardsByTitle()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "Lane management" });
        vm.Cards.Add(new CardViewModel { Title = "Card detail" });

        vm.ApplyFilter("lane");

        vm.FilteredCards.Should().HaveCount(1);
        vm.FilteredCards[0].Title.Should().Be("Lane management");
    }

    [Fact]
    public void ApplyFilter_EmptyString_ShowsAllCards()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.Cards.Add(new CardViewModel { Title = "B" });
        vm.ApplyFilter("A");

        vm.ApplyFilter(string.Empty);

        vm.FilteredCards.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyFilter_Null_ShowsAllCards()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.Cards.Add(new CardViewModel { Title = "B" });
        vm.ApplyFilter("A");

        vm.ApplyFilter(null!);

        vm.FilteredCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConfirmAddCardAsync_DoesNothingForBlankTitle()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(mediator: mediator);
        vm.NewCardTitle = "   ";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        await mediator.DidNotReceive().Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAddCardAsync_SendsCommandAndRefreshesBoard()
    {
        var refreshed = false;
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(default(Card?));
        var vm = NewVm(mediator: mediator, refreshBoard: () => { refreshed = true; return Task.CompletedTask; });
        vm.NewCardTitle = "New card";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        vm.IsAddingCard.Should().BeFalse();
        vm.NewCardTitle.Should().BeEmpty();
        refreshed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAddCardAsync_SetsErrorAndKeepsFormOpenOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new InvalidOperationException("DB error")));
        var vm = NewVm(mediator: mediator);
        vm.BeginAddCardCommand.Execute(null); // opens the add form
        vm.NewCardTitle = "New card";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        vm.AddCardErrorMessage.Should().Be("DB error");
        vm.IsAddingCard.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAddCardAsync_SetsErrorForNonInvalidOperationException()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new OperationCanceledException("Cancelled by user")));
        var vm = NewVm(mediator: mediator);
        vm.BeginAddCardCommand.Execute(null);
        vm.NewCardTitle = "New card";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        vm.AddCardErrorMessage.Should().Be("Cancelled by user");
        vm.IsAddingCard.Should().BeTrue();
    }

    [Fact]
    public void RebuildLaneItems_StandaloneCards_AreAddedDirectly()
    {
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.Cards.Add(new CardViewModel { Title = "B" });

        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        vm.LaneItems.Should().HaveCount(2);
        vm.LaneItems[0].Should().BeOfType<CardViewModel>().Which.Title.Should().Be("A");
        vm.LaneItems[1].Should().BeOfType<CardViewModel>().Which.Title.Should().Be("B");
    }

    [Fact]
    public void RebuildLaneItems_CardsSharingBatchId_AreGrouped()
    {
        var batchId = Guid.NewGuid();
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "Card 1", BatchId = batchId, BatchName = "MyBatch" });
        vm.Cards.Add(new CardViewModel { Title = "Card 2", BatchId = batchId, BatchName = "MyBatch" });

        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        vm.LaneItems.Should().HaveCount(1);
        var group = vm.LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.BatchId.Should().Be(batchId);
        group.Cards.Should().HaveCount(2);
    }

    [Fact]
    public void RebuildLaneItems_MultipleBatches_EachGetOwnGroup()
    {
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "A1", BatchId = batchA });
        vm.Cards.Add(new CardViewModel { Title = "B1", BatchId = batchB });
        vm.Cards.Add(new CardViewModel { Title = "A2", BatchId = batchA });

        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        vm.LaneItems.Should().HaveCount(2);
        vm.LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Which.Cards.Should().HaveCount(2);
        vm.LaneItems[1].Should().BeOfType<BatchGroupViewModel>().Which.Cards.Should().HaveCount(1);
    }

    [Fact]
    public void RebuildLaneItems_BatchStats_AreAppliedToGroup()
    {
        var batchId = Guid.NewGuid();
        var stats = new Dictionary<Guid, BatchStats> { [batchId] = new BatchStats("Sprint 1", 3, 1) };
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "C1", BatchId = batchId });

        vm.RebuildLaneItems(stats);

        var group = vm.LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.BatchName.Should().Be("Sprint 1");
        group.TotalCount.Should().Be(3);
        group.DoneCount.Should().Be(1);
        group.ProgressDisplay.Should().Be("(1/3)");
    }

    [Fact]
    public void RebuildLaneItems_MixedItems_OrderMatchesFilteredCards()
    {
        var batchId = Guid.NewGuid();
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "Standalone" });
        vm.Cards.Add(new CardViewModel { Title = "Batch-1", BatchId = batchId });
        vm.Cards.Add(new CardViewModel { Title = "Batch-2", BatchId = batchId });

        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        vm.LaneItems.Should().HaveCount(2);
        vm.LaneItems[0].Should().BeOfType<CardViewModel>().Which.Title.Should().Be("Standalone");
        vm.LaneItems[1].Should().BeOfType<BatchGroupViewModel>();
    }

    [Fact]
    public void RebuildLaneItems_ReusesExistingGroupVm_WhenBatchIdMatches()
    {
        var batchId = Guid.NewGuid();
        var vm = NewVm();
        vm.Cards.Add(new CardViewModel { Title = "C1", BatchId = batchId });
        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        var firstGroup = vm.LaneItems[0] as BatchGroupViewModel;

        vm.RebuildLaneItems(new Dictionary<Guid, BatchStats>());

        vm.LaneItems[0].Should().BeSameAs(firstGroup);
    }

    private static LaneViewModel NewVm(
        string name = "Doing",
        IMediator? mediator = null,
        Func<Task>? refreshBoard = null) =>
        new(mediator ?? Substitute.For<IMediator>(), refreshBoard ?? (() => Task.CompletedTask))
        {
            WorkspaceId = Guid.NewGuid(),
            Name = name,
        };
}

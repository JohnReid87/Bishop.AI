using Bishop.App.Batches.ListBatches;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class WorkspaceBatchesViewModelTests
{
    private static IMediator MediatorReturning(IReadOnlyList<BatchSummary> summaries)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(summaries);
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TagInfo>)[]);
        return mediator;
    }

    private static BatchSummary Summary(string name = "batch-1", int cardCount = 2, DateTimeOffset? finishedAt = null,
        IReadOnlyList<Card>? cards = null) =>
        new(new Batch
        {
            Id = Guid.NewGuid(),
            Name = name,
            BranchName = "feat/batch-1",
            Status = BatchStatus.Open,
        }, cardCount, finishedAt, false, false, false, cards ?? []);

    [Fact]
    public void Batches_Empty_Initially()
    {
        var vm = new WorkspaceBatchesViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public void HasBatches_False_Initially()
    {
        var vm = new WorkspaceBatchesViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        vm.HasBatches.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_PopulatesBatches_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2")]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesTrue_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.HasBatches.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_LeavesEmptyBatches_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesFalse_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.HasBatches.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_MapsBatchSummaryPropertiesToViewModel()
    {
        var id = Guid.NewGuid();
        var batch = new Batch
        {
            Id = id,
            Name = "my-batch",
            BranchName = "feat/my-branch",
            Status = BatchStatus.Working,
        };
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([new BatchSummary(batch, 5, null, false, false, false, [])]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        var item = vm.Batches.Single();
        item.Id.Should().Be(id);
        item.Name.Should().Be("my-batch");
        item.BranchName.Should().Be("feat/my-branch");
        item.Status.Should().Be(BatchStatus.Working);
        item.CardCount.Should().Be(5);
    }

    [Fact]
    public async Task LoadAsync_ClearsAndRepopulates_OnSecondCall()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                [Summary("first")],
                [Summary("second-a"), Summary("second-b")]);
        var vm = new WorkspaceBatchesViewModel(mediator, Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);
        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().HaveCount(2);
        vm.Batches.Select(b => b.Name).Should().Equal("second-a", "second-b");
    }

    [Fact]
    public async Task RefreshCommand_PopulatesBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Batches.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_BadgeIsNotVisible_WhenNoBatchesHaveFinishedAt()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2")]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeIsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_BadgeIsVisible_WhenAtLeastOneBatchHasFinishedAt()
    {
        var finished = DateTimeOffset.UtcNow;
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2", finishedAt: finished)]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeIsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_BadgeCount_MatchesFinishedBatchCount()
    {
        var finished = DateTimeOffset.UtcNow;
        var vm = new WorkspaceBatchesViewModel(MediatorReturning(
        [
            Summary("batch-1"),
            Summary("batch-2", finishedAt: finished),
            Summary("batch-3", finishedAt: finished),
        ]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadAsync_BadgeColor_IsYellow_WhenReadyBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(finishedAt: DateTimeOffset.UtcNow)]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeColor.Should().Be("#c4a85f");
    }

    [Fact]
    public async Task LoadAsync_BadgeColor_IsEmpty_WhenNoReadyBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeColor.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_BadgeTooltip_DescribesReadyCount()
    {
        var finished = DateTimeOffset.UtcNow;
        var vm = new WorkspaceBatchesViewModel(MediatorReturning(
        [
            Summary("batch-1", finishedAt: finished),
            Summary("batch-2"),
        ]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeTooltip.Should().Be("1 of 2 batches ready to complete");
    }

    [Fact]
    public async Task LoadAsync_BadgeTooltip_IsEmpty_WhenNoReadyBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeTooltip.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_BadgeClears_AfterRefreshWithNoFinishedBatches()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                [Summary(finishedAt: DateTimeOffset.UtcNow)],
                [Summary()]);
        var vm = new WorkspaceBatchesViewModel(mediator, Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);
        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeIsVisible.Should().BeFalse();
        vm.BadgeCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_MapsGitStateToViewModel()
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            Name = "my-batch",
            BranchName = "feat/my-branch",
            Status = BatchStatus.Working,
        };
        var summary = new BatchSummary(batch, 0, null, IsMerged: true, BranchExists: true, WorktreeExists: false, Cards: []);
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        var item = vm.Batches.Single();
        item.IsMerged.Should().BeTrue();
        item.BranchExists.Should().BeTrue();
        item.WorktreeExists.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_PopulatesCardsOnBatchItemViewModel()
    {
        var card = new Card { Id = Guid.NewGuid(), Number = 7, Title = "My card", LaneName = "To Do" };
        var batch = new Batch { Id = Guid.NewGuid(), Name = "b", Status = BatchStatus.Open };
        var summary = new BatchSummary(batch, 1, null, false, false, false, [card]);
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        var batchVm = vm.Batches.Single();
        batchVm.Cards.Should().HaveCount(1);
        batchVm.Cards[0].Number.Should().Be(7);
        batchVm.Cards[0].Title.Should().Be("My card");
    }

    [Fact]
    public async Task LoadAsync_MarksInProgressCard_LowestDoingCardWithNoFailure()
    {
        var doingCard = new Card { Id = Guid.NewGuid(), Number = 2, LaneName = "Doing", LastAutoRunFailedAt = null };
        var toDoCard = new Card { Id = Guid.NewGuid(), Number = 3, LaneName = "To Do" };
        var batch = new Batch { Id = Guid.NewGuid(), Name = "b", Status = BatchStatus.Working };
        var summary = new BatchSummary(batch, 2, null, false, false, false, [doingCard, toDoCard]);
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        var batchVm = vm.Batches.Single();
        batchVm.Cards.Single(c => c.Id == doingCard.Id).IsInProgress.Should().BeTrue();
        batchVm.Cards.Single(c => c.Id == toDoCard.Id).IsInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_DoesNotMarkInProgressCard_WhenBatchIsNotWorking()
    {
        var doingCard = new Card { Id = Guid.NewGuid(), Number = 1, LaneName = "Doing", LastAutoRunFailedAt = null };
        var batch = new Batch { Id = Guid.NewGuid(), Name = "b", Status = BatchStatus.Open };
        var summary = new BatchSummary(batch, 1, null, false, false, false, [doingCard]);
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Single().Cards.Single().IsInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_DoesNotMarkInProgressCard_WhenDoingCardHasLastAutoRunFailedAt()
    {
        var failedCard = new Card
        {
            Id = Guid.NewGuid(),
            Number = 1,
            LaneName = "Doing",
            LastAutoRunFailedAt = DateTimeOffset.UtcNow,
        };
        var batch = new Batch { Id = Guid.NewGuid(), Name = "b", Status = BatchStatus.Working };
        var summary = new BatchSummary(batch, 1, null, false, false, false, [failedCard]);
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]), Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>());

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Single().Cards.Single().IsInProgress.Should().BeFalse();
    }

    // ── New delegating methods ────────────────────────────────────────────────

    private static (WorkspaceBatchesViewModel vm, IMediator mediator, Bishop.App.Services.Terminal.ITerminalLauncher launcher) MakeVm()
    {
        var mediator = MediatorReturning([]);
        var launcher = Substitute.For<Bishop.App.Services.Terminal.ITerminalLauncher>();
        return (new WorkspaceBatchesViewModel(mediator, launcher), mediator, launcher);
    }

    [Fact]
    public async Task RequestStopAsync_SendsRequestStopBatchCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var batchId = Guid.NewGuid();
        await vm.RequestStopAsync(batchId);

        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.RequestStopBatch.RequestStopBatchCommand>(c => c.BatchId == batchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MergeAsync_SendsMergeBatchCommand_ReturnsResult()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<Bishop.App.Batches.MergeBatch.MergeBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.App.Batches.MergeBatch.MergeBatchResult(true, [], null));

        var result = await vm.MergeAsync("my-batch", @"C:\repo");

        result.Success.Should().BeTrue();
        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.MergeBatch.MergeBatchCommand>(c => c.Name == "my-batch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_SendsRemoveBatchCommand()
    {
        var (vm, mediator, _) = MakeVm();
        await vm.RemoveAsync("my-batch");

        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.RemoveBatch.RemoveBatchCommand>(c => c.Name == "my-batch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_SendsRenameBatchCommand_ReturnsNewName()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<Bishop.App.Batches.RenameBatch.RenameBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.Core.Batch { Name = "new-name" });

        var result = await vm.RenameAsync("old-name", "new-name");

        result.Should().Be("new-name");
    }

    [Fact]
    public void LaunchBatch_CallsTerminalLauncherLaunchCommand()
    {
        var (vm, _, launcher) = MakeVm();

        vm.LaunchBatch(@"C:\repo", "my-batch", new Bishop.App.Services.Terminal.TerminalSnap());

        launcher.Received(1).LaunchCommand(
            @"C:\repo", "bishop",
            Arg.Is<string[]>(args => args.Contains("run") && args.Contains("my-batch")),
            Arg.Any<Bishop.App.Services.Terminal.TerminalSnap?>());
    }

    [Fact]
    public void ResumeBatch_CallsTerminalLauncherWithResumeFlag()
    {
        var (vm, _, launcher) = MakeVm();

        vm.ResumeBatch(@"C:\repo", "my-batch", new Bishop.App.Services.Terminal.TerminalSnap());

        launcher.Received(1).LaunchCommand(
            @"C:\repo", "bishop",
            Arg.Is<string[]>(args => args.Contains("--resume")),
            Arg.Any<Bishop.App.Services.Terminal.TerminalSnap?>());
    }
}

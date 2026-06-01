using Bishop.App.Batches.LaunchBatchTerminal;
using Bishop.App.Batches.ListBatches;
using Bishop.App.Batches.RemoveCardFromBatch;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Tags.ListTags;
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
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public class WorkspaceBatchesViewModelTests
{
    private static ISender MediatorReturning(IReadOnlyList<BatchSummary> summaries)
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(summaries);
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
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
        var vm = new WorkspaceBatchesViewModel(Substitute.For<ISender>());

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public void HasBatches_False_Initially()
    {
        var vm = new WorkspaceBatchesViewModel(Substitute.For<ISender>());

        vm.HasBatches.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_PopulatesBatches_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2")]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesTrue_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.HasBatches.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_LeavesEmptyBatches_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesFalse_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]));

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([new BatchSummary(batch, 5, null, false, false, false, [])]));

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
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                [Summary("first")],
                [Summary("second-a"), Summary("second-b")]);
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TagInfo>)[]);
        var vm = new WorkspaceBatchesViewModel(mediator);

        await vm.LoadAsync(Guid.Empty, string.Empty);
        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Should().HaveCount(2);
        vm.Batches.Select(b => b.Name).Should().Equal("second-a", "second-b");
    }

    [Fact]
    public async Task RefreshCommand_PopulatesBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]));

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Batches.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_BadgeIsNotVisible_WhenNoBatchesHaveFinishedAt()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2")]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeIsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_BadgeIsVisible_WhenAtLeastOneBatchHasFinishedAt()
    {
        var finished = DateTimeOffset.UtcNow;
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2", finishedAt: finished)]));

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
        ]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadAsync_BadgeColor_IsYellow_WhenReadyBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(finishedAt: DateTimeOffset.UtcNow)]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeColor.Should().Be("#c4a85f");
    }

    [Fact]
    public async Task LoadAsync_BadgeColor_IsEmpty_WhenNoReadyBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]));

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
        ]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeTooltip.Should().Be("1 of 2 batches ready to complete");
    }

    [Fact]
    public async Task LoadAsync_BadgeTooltip_IsEmpty_WhenNoReadyBatches()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.BadgeTooltip.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_BadgeClears_AfterRefreshWithNoFinishedBatches()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                [Summary(finishedAt: DateTimeOffset.UtcNow)],
                [Summary()]);
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TagInfo>)[]);
        var vm = new WorkspaceBatchesViewModel(mediator);

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]));

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]));

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]));

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]));

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
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([summary]));

        await vm.LoadAsync(Guid.Empty, string.Empty);

        vm.Batches.Single().Cards.Single().IsInProgress.Should().BeFalse();
    }

    // ── New delegating methods ────────────────────────────────────────────────

    private static (WorkspaceBatchesViewModel vm, ISender mediator) MakeVm()
    {
        var mediator = MediatorReturning([]);
        return (new WorkspaceBatchesViewModel(mediator), mediator);
    }

    [Fact]
    public async Task RequestStopAsync_SendsRequestStopBatchCommand()
    {
        var (vm, mediator) = MakeVm();
        var batchId = Guid.NewGuid();
        await vm.RequestStopAsync(batchId);

        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.RequestStopBatch.RequestStopBatchCommand>(c => c.BatchId == batchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MergeAsync_SendsMergeBatchCommand_MapsSuccessResult()
    {
        var (vm, mediator) = MakeVm();
        mediator.Send(Arg.Any<Bishop.App.Batches.MergeBatch.MergeBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.App.Batches.MergeBatch.MergeBatchResult(true, [], null));

        var result = await vm.MergeAsync("my-batch", @"C:\repo");

        result.Should().BeOfType<BatchMergeOutcome>();
        result.Success.Should().BeTrue();
        result.ConflictFiles.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.MergeBatch.MergeBatchCommand>(c => c.Name == "my-batch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MergeAsync_Conflict_MapsConflictFilesAndError()
    {
        var (vm, mediator) = MakeVm();
        mediator.Send(Arg.Any<Bishop.App.Batches.MergeBatch.MergeBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.App.Batches.MergeBatch.MergeBatchResult(
                false,
                ["src/A.cs", "src/B.cs"],
                "merge conflicts"));

        var result = await vm.MergeAsync("my-batch", @"C:\repo");

        result.Success.Should().BeFalse();
        result.ConflictFiles.Should().BeEquivalentTo(["src/A.cs", "src/B.cs"]);
        result.ErrorMessage.Should().Be("merge conflicts");
    }

    [Fact]
    public async Task RemoveAsync_SendsRemoveBatchCommand()
    {
        var (vm, mediator) = MakeVm();
        await vm.RemoveAsync("my-batch");

        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Batches.RemoveBatch.RemoveBatchCommand>(c => c.Name == "my-batch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_SendsRenameBatchCommand_ReturnsNewName()
    {
        var (vm, mediator) = MakeVm();
        mediator.Send(Arg.Any<Bishop.App.Batches.RenameBatch.RenameBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.Core.Batch { Name = "new-name" });

        var result = await vm.RenameAsync("old-name", "new-name");

        result.Should().Be("new-name");
    }

    [Fact]
    public async Task LaunchBatch_SendsLaunchBatchTerminalCommand_WithoutResume()
    {
        var (vm, mediator) = MakeVm();

        await vm.LaunchBatch(@"C:\repo", "my-batch", "claude-opus-4-7", new Bishop.App.Services.Terminal.TerminalSnap());

        await mediator.Received(1).Send(
            Arg.Is<LaunchBatchTerminalCommand>(c =>
                c.WorkspacePath == @"C:\repo"
                && c.BatchName == "my-batch"
                && c.Model == "claude-opus-4-7"
                && c.Resume == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeBatch_SendsLaunchBatchTerminalCommand_WithResume()
    {
        var (vm, mediator) = MakeVm();

        await vm.ResumeBatch(@"C:\repo", "my-batch", "claude-opus-4-7", new Bishop.App.Services.Terminal.TerminalSnap());

        await mediator.Received(1).Send(
            Arg.Is<LaunchBatchTerminalCommand>(c =>
                c.WorkspacePath == @"C:\repo"
                && c.BatchName == "my-batch"
                && c.Model == "claude-opus-4-7"
                && c.Resume == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkCardDoneAndResumeAsync_SendsMoveCardToDone_ThenResumesLaunch()
    {
        var (vm, mediator) = MakeVm();
        var cardId = Guid.NewGuid();
        var snap = new Bishop.App.Services.Terminal.TerminalSnap();

        await vm.MarkCardDoneAndResumeAsync(cardId, "my-batch", @"C:\repo", "claude-sonnet-4-6", snap);

        await mediator.Received(1).Send(
            Arg.Is<MoveCardCommand>(c => c.CardId == cardId && c.ToLaneName == SystemLaneNames.Done),
            Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<LaunchBatchTerminalCommand>(c =>
                c.BatchName == "my-batch"
                && c.WorkspacePath == @"C:\repo"
                && c.Resume == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCardAndResumeAsync_SendsRemoveCardFromBatch_ThenResumesLaunch()
    {
        var (vm, mediator) = MakeVm();
        var cardId = Guid.NewGuid();
        var snap = new Bishop.App.Services.Terminal.TerminalSnap();

        await vm.RemoveCardAndResumeAsync("my-batch", cardId, @"C:\repo", "claude-sonnet-4-6", snap);

        await mediator.Received(1).Send(
            Arg.Is<RemoveCardFromBatchCommand>(c => c.BatchName == "my-batch" && c.CardId == cardId),
            Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<LaunchBatchTerminalCommand>(c =>
                c.BatchName == "my-batch"
                && c.WorkspacePath == @"C:\repo"
                && c.Resume == true),
            Arg.Any<CancellationToken>());
    }
}

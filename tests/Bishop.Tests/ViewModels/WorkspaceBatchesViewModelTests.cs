using Bishop.App.Batches.ListBatches;
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
        return mediator;
    }

    private static BatchSummary Summary(string name = "batch-1", int cardCount = 2) =>
        new(new Batch
        {
            Id = Guid.NewGuid(),
            Name = name,
            BranchName = "feat/batch-1",
            Status = BatchStatus.Open,
            GitHubPrUrl = null,
        }, cardCount);

    [Fact]
    public void Batches_Empty_Initially()
    {
        var vm = new WorkspaceBatchesViewModel(Substitute.For<IMediator>());

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public void HasBatches_False_Initially()
    {
        var vm = new WorkspaceBatchesViewModel(Substitute.For<IMediator>());

        vm.HasBatches.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_PopulatesBatches_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary(), Summary("batch-2")]));

        await vm.LoadAsync();

        vm.Batches.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesTrue_WhenResultsReturned()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([Summary()]));

        await vm.LoadAsync();

        vm.HasBatches.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_LeavesEmptyBatches_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]));

        await vm.LoadAsync();

        vm.Batches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SetsHasBatchesFalse_WhenNoBatchesExist()
    {
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([]));

        await vm.LoadAsync();

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
            GitHubPrUrl = "https://github.com/owner/repo/pull/7",
        };
        var vm = new WorkspaceBatchesViewModel(MediatorReturning([new BatchSummary(batch, 5)]));

        await vm.LoadAsync();

        var item = vm.Batches.Single();
        item.Id.Should().Be(id);
        item.Name.Should().Be("my-batch");
        item.BranchName.Should().Be("feat/my-branch");
        item.Status.Should().Be(BatchStatus.Working);
        item.CardCount.Should().Be(5);
        item.GitHubPrUrl.Should().Be("https://github.com/owner/repo/pull/7");
    }

    [Fact]
    public async Task LoadAsync_ClearsAndRepopulates_OnSecondCall()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                [Summary("first")],
                [Summary("second-a"), Summary("second-b")]);
        var vm = new WorkspaceBatchesViewModel(mediator);

        await vm.LoadAsync();
        await vm.LoadAsync();

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
}

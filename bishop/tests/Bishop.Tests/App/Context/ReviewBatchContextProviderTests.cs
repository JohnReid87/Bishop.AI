using Bishop.App.Batches.GetBatch;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Findings.GetPriorFindings;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.App.Context;

public sealed class ReviewBatchContextProviderTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "ws", Path = "C:\\ws" };

    private static Batch MakeBatch(Guid workspaceId, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            BranchName = "bishop/" + name,
            BaseBranch = "main",
            Status = BatchStatus.Working,
            WorktreePath = "C:\\wt\\" + name,
            Model = "claude-sonnet-5",
        };

    [Fact]
    public async Task BuildSkillSpecificAsync_ReturnsBatchCardsAndPriorFindings()
    {
        var workspace = MakeWorkspace();
        var batch = MakeBatch(workspace.Id, "delivery-1");
        var cards = new List<Card>
        {
            new() { Number = 10, Title = "Card ten", Description = "### Acceptance\n- does X", TagName = "feature", LaneName = "Doing" },
        };
        var prior = new List<PriorFindingRecord>
        {
            new("hash1", null, "src/F.cs", "Sym", "Correctness", "Title", "dismissed", "Not real.", null),
        };

        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Is<GetBatchQuery>(q => q.Name == "delivery-1"), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(batch, cards));
        mediator.Send(
                Arg.Is<GetPriorFindingsQuery>(q => q.SkillName == "bish-review-batch"
                    && q.WorkspaceId == workspace.Id
                    && q.BatchId == batch.Id),
                Arg.Any<CancellationToken>())
            .Returns(prior);

        var sut = new ReviewBatchContextProvider();

        var result = await sut.BuildSkillSpecificAsync(
            new ContextPackArgs(null, "delivery-1"), workspace, mediator, default);

        result.Should().NotBeNull();
        var type = result!.GetType();

        var priorProp = type.GetProperty("priorFindings")!.GetValue(result);
        priorProp.Should().BeEquivalentTo(prior);

        var cardsProp = (System.Collections.IEnumerable)type.GetProperty("cards")!.GetValue(result)!;
        cardsProp.Cast<object>().Should().HaveCount(1);

        var batchProp = type.GetProperty("batch")!.GetValue(result)!;
        batchProp.GetType().GetProperty("name")!.GetValue(batchProp).Should().Be("delivery-1");
    }

    [Fact]
    public async Task BuildSkillSpecificAsync_QueriesPriorFindingsScopedToTheBatch()
    {
        var workspace = MakeWorkspace();
        var batch = MakeBatch(workspace.Id, "delivery-2");
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(batch, Array.Empty<Card>()));
        mediator.Send(Arg.Any<GetPriorFindingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PriorFindingRecord>());

        var sut = new ReviewBatchContextProvider();

        await sut.BuildSkillSpecificAsync(new ContextPackArgs(null, "delivery-2"), workspace, mediator, default);

        await mediator.Received(1).Send(
            Arg.Is<GetPriorFindingsQuery>(q => q.BatchId == batch.Id && q.SkillName == "bish-review-batch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildSkillSpecificAsync_ThrowsWhenBatchArgMissing()
    {
        var workspace = MakeWorkspace();
        var mediator = Substitute.For<ISender>();
        var sut = new ReviewBatchContextProvider();

        var act = () => sut.BuildSkillSpecificAsync(new ContextPackArgs(null), workspace, mediator, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*--batch*");
    }
}

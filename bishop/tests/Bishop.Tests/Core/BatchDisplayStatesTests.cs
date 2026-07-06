using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.Core;

public class BatchDisplayStatesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void OpenBatch_WithNothingDone_IsOpen()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Open, finishedAt: null, mergedAt: null, allCardsDone: false);

        state.Should().Be(BatchDisplayState.Open);
    }

    [Fact]
    public void WorkingBatch_MidRun_IsWorking()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Working, finishedAt: null, mergedAt: null, allCardsDone: false);

        state.Should().Be(BatchDisplayState.Working);
    }

    [Fact]
    public void WorkingBatch_WithFinishedAt_IsFinished()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Working, finishedAt: Now, mergedAt: null, allCardsDone: false);

        state.Should().Be(BatchDisplayState.Finished);
    }

    [Fact]
    public void HandWorkedOpenBatch_WithAllCardsDone_IsFinished()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Open, finishedAt: null, mergedAt: null, allCardsDone: true);

        state.Should().Be(BatchDisplayState.Finished);
    }

    [Fact]
    public void MergedNotClosedBatch_IsMerged()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Working, finishedAt: Now, mergedAt: Now, allCardsDone: true);

        state.Should().Be(BatchDisplayState.Merged);
    }

    [Fact]
    public void ClosedBatch_IsClosed_EvenWhenMerged()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Closed, finishedAt: Now, mergedAt: Now, allCardsDone: true);

        state.Should().Be(BatchDisplayState.Closed);
    }

    [Fact]
    public void ClosedBatch_IsClosed()
    {
        var state = BatchDisplayStates.Derive(BatchStatus.Closed, finishedAt: null, mergedAt: null, allCardsDone: false);

        state.Should().Be(BatchDisplayState.Closed);
    }
}

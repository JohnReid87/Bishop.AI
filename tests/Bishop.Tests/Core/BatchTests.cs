using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.Core;

public sealed class BatchTests
{
    private static Batch OpenBatch() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        Name = "my-batch",
        BranchName = "bishop/my-batch",
        BaseBranch = "main",
        Status = BatchStatus.Open,
        CreatedAt = DateTimeOffset.UtcNow,
        WorktreePath = @"C:\worktrees\my-batch",
    };

    // ── TransitionToWorking ────────────────────────────────────────────────────

    [Fact]
    public void TransitionToWorking_WhenOpen_SetsStatusToWorking()
    {
        var batch = OpenBatch();

        batch.TransitionToWorking();

        batch.Status.Should().Be(BatchStatus.Working);
    }

    [Fact]
    public void TransitionToWorking_WhenWorking_Throws()
    {
        var batch = OpenBatch();
        batch.Status = BatchStatus.Working;

        var act = () => batch.TransitionToWorking();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be Open*");
    }

    [Fact]
    public void TransitionToWorking_WhenClosed_Throws()
    {
        var batch = OpenBatch();
        batch.Status = BatchStatus.Closed;

        var act = () => batch.TransitionToWorking();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be Open*");
    }

    // ── Close ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Close_WhenOpen_SetsStatusAndFields()
    {
        var batch = OpenBatch();
        var now = DateTimeOffset.UtcNow;

        batch.Close(BatchClosedReason.Finished, now);

        batch.Status.Should().Be(BatchStatus.Closed);
        batch.ClosedReason.Should().Be(BatchClosedReason.Finished);
        batch.ClosedAt.Should().Be(now);
    }

    [Fact]
    public void Close_WhenWorking_SetsStatusAndFields()
    {
        var batch = OpenBatch();
        batch.Status = BatchStatus.Working;
        var now = DateTimeOffset.UtcNow;

        batch.Close(BatchClosedReason.Abandoned, now);

        batch.Status.Should().Be(BatchStatus.Closed);
        batch.ClosedReason.Should().Be(BatchClosedReason.Abandoned);
        batch.ClosedAt.Should().Be(now);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_Throws()
    {
        var batch = OpenBatch();
        batch.Status = BatchStatus.Closed;

        var act = () => batch.Close(BatchClosedReason.Finished, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already Closed*");
    }
}

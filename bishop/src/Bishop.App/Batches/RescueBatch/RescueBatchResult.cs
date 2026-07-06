namespace Bishop.App.Batches.RescueBatch;

public enum RescueBatchOutcome
{
    /// <summary>The interrupted run was recovered (any combination of lock cleared, worktree reset, cards re-queued).</summary>
    Rescued,

    /// <summary>The lock is held by a still-running process; rescue refused to touch the batch.</summary>
    LockAlive,

    /// <summary>The batch is not in a Working state, so there is no interrupted run to rescue.</summary>
    NotRunning,

    /// <summary>The worktree is dirty and the caller did not confirm the destructive reset.</summary>
    NeedsConfirmation,
}

public sealed record RescueBatchResult(
    RescueBatchOutcome Outcome,
    int? LockOwnerPid = null,
    bool LockCleared = false,
    bool WorktreeReset = false,
    IReadOnlyList<string>? DirtyPaths = null,
    IReadOnlyList<int>? RequeuedCardNumbers = null);

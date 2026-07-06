namespace Bishop.App.Batches.SalvageBatch;

public enum SalvageBatchOutcome
{
    /// <summary>The merge/eject split was computed but no action was taken; the caller must confirm.</summary>
    NeedsConfirmation,

    /// <summary>The lock is held by a still-running process; salvage refused to touch the batch.</summary>
    LockAlive,

    /// <summary>No card finished, so there is no prefix to deliver — the caller should abandon instead.</summary>
    NothingSucceeded,

    /// <summary>The merge conflicted and was aborted; no DB or worktree state was changed.</summary>
    MergeConflict,

    /// <summary>The succeeded prefix was merged, the rest ejected to To Do, and the batch closed.</summary>
    Salvaged,
}

public sealed record SalvageBatchResult(
    SalvageBatchOutcome Outcome,
    int? LockOwnerPid = null,
    IReadOnlyList<int>? MergedCardNumbers = null,
    IReadOnlyList<int>? EjectedCardNumbers = null,
    IReadOnlyList<int>? ClosedCardNumbers = null,
    IReadOnlyList<string>? ConflictFiles = null,
    string? ErrorMessage = null);

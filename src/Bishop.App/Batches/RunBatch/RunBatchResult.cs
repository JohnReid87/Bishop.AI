namespace Bishop.App.Batches.RunBatch;

public enum RunBatchStopReason
{
    Finished,
    CardFailure,
    HandoffMissing,
    DirtyWorktree,
    NotAGitRepo,
    GitNotFound,
}

public sealed record RunBatchResult(
    int Succeeded,
    IReadOnlyList<int>? FailedCardNumbers,
    RunBatchStopReason StopReason,
    IReadOnlyList<string>? DirtyPaths = null);

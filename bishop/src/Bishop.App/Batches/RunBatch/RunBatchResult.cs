namespace Bishop.App.Batches.RunBatch;

public enum RunBatchStopReason
{
    Finished,
    CardFailure,
    HandoffMissing,
    HandoffMalformed,
    DirtyWorktree,
    NotAGitRepo,
    GitNotFound,
    StopRequested,
}

public sealed record RunBatchResult(
    int Succeeded,
    IReadOnlyList<int>? FailedCardNumbers,
    RunBatchStopReason StopReason,
    IReadOnlyList<string>? DirtyPaths = null);

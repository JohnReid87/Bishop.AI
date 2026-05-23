namespace Bishop.App.WorkNext;

public enum WorkNextStopReason
{
    EmptyLane,
    CapReached,
    DirtyWorkingTree,
    NotAGitRepo,
    GitNotFound,
    Cancelled,
}

public sealed record WorkNextResult(
    int Succeeded,
    WorkNextStopReason StopReason,
    IReadOnlyList<int>? FailedCardNumbers = null,
    IReadOnlyList<string>? DirtyPaths = null);

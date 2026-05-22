namespace Bishop.App.WorkNext;

public enum WorkNextStopReason
{
    EmptyLane,
    CapReached,
    DirtyWorkingTree,
    ClaudeFailed,
    NotAGitRepo,
    GitNotFound,
    Cancelled,
}

public sealed record WorkNextResult(
    int CardsProcessed,
    WorkNextStopReason StopReason,
    int? FailedCardNumber = null,
    IReadOnlyList<string>? DirtyPaths = null);

namespace Bishop.ViewModels.Batches;

// Presentation-layer mirror of Bishop.App's RescueBatchOutcome, so code-behind can branch on the
// result without referencing Bishop.App (see the thin-code-behind rule in CONTEXT.md).
public enum BatchRescueOutcome
{
    Rescued,
    LockAlive,
    NotRunning,
    NeedsConfirmation,
}

public sealed record BatchRescueResult(
    BatchRescueOutcome Outcome,
    int? LockOwnerPid = null,
    IReadOnlyList<string>? DirtyPaths = null,
    IReadOnlyList<int>? RequeuedCardNumbers = null);

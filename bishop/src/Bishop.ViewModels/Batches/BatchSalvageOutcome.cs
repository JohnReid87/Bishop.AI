namespace Bishop.ViewModels.Batches;

// Presentation-layer mirror of Bishop.App's SalvageBatchOutcome, so code-behind can branch on the
// result without referencing Bishop.App (see the thin-code-behind rule in CONTEXT.md).
public enum BatchSalvageOutcome
{
    NeedsConfirmation,
    LockAlive,
    NothingSucceeded,
    MergeConflict,
    Salvaged,
}

public sealed record BatchSalvageResult(
    BatchSalvageOutcome Outcome,
    int? LockOwnerPid = null,
    IReadOnlyList<int>? MergedCardNumbers = null,
    IReadOnlyList<int>? EjectedCardNumbers = null,
    IReadOnlyList<int>? ClosedCardNumbers = null,
    IReadOnlyList<string>? ConflictFiles = null,
    string? ErrorMessage = null);

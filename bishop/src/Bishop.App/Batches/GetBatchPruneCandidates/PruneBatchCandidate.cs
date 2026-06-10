using Bishop.Core;

namespace Bishop.App.Batches.GetBatchPruneCandidates;

public sealed record PruneBatchCandidate(
    string BatchName,
    string BranchName,
    BatchClosedReason ClosedReason,
    DateTimeOffset ClosedAt,
    int CommitCount,
    bool IsCheckedOut
);

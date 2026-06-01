using Bishop.Core;

namespace Bishop.ViewModels.Batches;

internal readonly record struct BatchItemActions(
    bool CanRun,
    bool CanPause,
    bool CanResume,
    bool CanMerge,
    bool CanCleanUp,
    bool CanAbandon,
    bool CanRemove)
{
    internal static BatchItemActions For(
        BatchStatus status,
        DateTimeOffset? finishedAt,
        DateTimeOffset? stoppedAt,
        bool isMerged,
        bool branchExists,
        bool worktreeExists) =>
        new(
            CanRun: status == BatchStatus.Open,
            CanPause: status == BatchStatus.Working && finishedAt is null && stoppedAt is null,
            CanResume: status == BatchStatus.Working && finishedAt is null && stoppedAt is not null,
            CanMerge: status == BatchStatus.Working && finishedAt is not null && !isMerged,
            CanCleanUp: isMerged && (branchExists || worktreeExists),
            CanAbandon: status != BatchStatus.Closed && !isMerged,
            CanRemove: status == BatchStatus.Closed);
}

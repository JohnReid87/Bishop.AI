using MediatR;

namespace Bishop.App.Batches.GetBatchPruneCandidates;

public sealed record GetBatchPruneCandidatesQuery(
    Guid WorkspaceId,
    string WorkspacePath,
    bool AbandonedOnly,
    bool MergedOnly,
    TimeSpan? OlderThan
) : IRequest<IReadOnlyList<PruneBatchCandidate>>;

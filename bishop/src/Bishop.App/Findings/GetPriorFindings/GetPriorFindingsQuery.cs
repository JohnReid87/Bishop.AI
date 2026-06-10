using MediatR;

namespace Bishop.App.Findings.GetPriorFindings;

internal sealed record GetPriorFindingsQuery(
    Guid WorkspaceId,
    string SkillName) : IRequest<IReadOnlyList<PriorFindingRecord>>;

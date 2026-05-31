using MediatR;

namespace Bishop.App.Findings.GetPriorFindings;

public sealed record GetPriorFindingsQuery(
    Guid WorkspaceId,
    string SkillName) : IRequest<IReadOnlyList<PriorFindingRecord>>;

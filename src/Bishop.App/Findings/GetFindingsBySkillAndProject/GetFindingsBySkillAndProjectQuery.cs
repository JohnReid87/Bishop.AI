using Bishop.Core;
using MediatR;

namespace Bishop.App.Findings.GetFindingsBySkillAndProject;

public sealed record GetFindingsBySkillAndProjectQuery(
    Guid WorkspaceId,
    string SkillName,
    string? ProjectName) : IRequest<IReadOnlyList<FindingRecord>>;

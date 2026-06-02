using Bishop.Core;
using MediatR;

namespace Bishop.App.Findings.GetFindingsBySkillAndProject;

internal sealed record GetFindingsBySkillAndProjectQuery(
    Guid WorkspaceId,
    string SkillName,
    string? ProjectName) : IRequest<IReadOnlyList<FindingRecord>>;

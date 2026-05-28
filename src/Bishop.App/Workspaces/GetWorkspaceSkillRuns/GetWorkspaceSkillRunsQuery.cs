using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.GetWorkspaceSkillRuns;

public sealed record GetWorkspaceSkillRunsQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<WorkspaceSkillRun>>;

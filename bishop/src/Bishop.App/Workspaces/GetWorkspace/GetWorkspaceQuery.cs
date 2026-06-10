using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.GetWorkspace;

public sealed record GetWorkspaceQuery(Guid Id) : IRequest<Workspace?>;

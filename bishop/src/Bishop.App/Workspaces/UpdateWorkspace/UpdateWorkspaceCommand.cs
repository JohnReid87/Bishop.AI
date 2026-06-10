using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.UpdateWorkspace;

public sealed record UpdateWorkspaceCommand(Guid Id, string Name, string Path) : IRequest<Workspace>;

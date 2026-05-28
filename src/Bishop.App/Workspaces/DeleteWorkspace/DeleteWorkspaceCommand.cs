using MediatR;

namespace Bishop.App.Workspaces.DeleteWorkspace;

public sealed record DeleteWorkspaceCommand(Guid Id) : IRequest<Unit>;

using MediatR;

namespace Bishop.App.Workspaces.RemoveWorkspace;

public sealed record RemoveWorkspaceCommand(Guid Id) : IRequest;

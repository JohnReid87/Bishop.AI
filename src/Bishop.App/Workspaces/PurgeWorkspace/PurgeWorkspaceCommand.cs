using MediatR;

namespace Bishop.App.Workspaces.PurgeWorkspace;

public sealed record PurgeWorkspaceCommand(Guid Id) : IRequest<Unit>;

using MediatR;

namespace Bishop.App.Workspaces.ReorderWorkspaces;

public sealed record ReorderWorkspacesCommand(IReadOnlyList<Guid> OrderedIds) : IRequest<Unit>;

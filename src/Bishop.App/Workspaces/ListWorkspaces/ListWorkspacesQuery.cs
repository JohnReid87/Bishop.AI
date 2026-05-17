using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.ListWorkspaces;

public sealed record ListWorkspacesQuery : IRequest<IReadOnlyList<Workspace>>;

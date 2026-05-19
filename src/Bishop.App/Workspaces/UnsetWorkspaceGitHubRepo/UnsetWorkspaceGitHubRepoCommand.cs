using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;

public sealed record UnsetWorkspaceGitHubRepoCommand(Guid WorkspaceId) : IRequest<Workspace>;

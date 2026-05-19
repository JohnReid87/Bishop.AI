using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.SetWorkspaceGitHubRepo;

public sealed record SetWorkspaceGitHubRepoCommand(Guid WorkspaceId, string Repo) : IRequest<Workspace>;

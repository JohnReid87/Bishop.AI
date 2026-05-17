using Bishop.Core;
using MediatR;

namespace Bishop.App.Workspaces.CreateWorkspace;

public sealed record CreateWorkspaceCommand(string Name, string Path, bool InitGit = false) : IRequest<Workspace>;

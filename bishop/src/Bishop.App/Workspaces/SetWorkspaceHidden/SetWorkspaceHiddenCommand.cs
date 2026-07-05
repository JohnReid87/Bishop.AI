using MediatR;

namespace Bishop.App.Workspaces.SetWorkspaceHidden;

public sealed record SetWorkspaceHiddenCommand(Guid Id, bool Hidden) : IRequest;

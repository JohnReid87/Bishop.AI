using MediatR;

namespace Bishop.App.Git.Push;

public sealed record PushCommand(string WorkspacePath, bool SetUpstream = false) : IRequest<PushResult>;

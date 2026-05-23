using MediatR;

namespace Bishop.App.Git.Push;

public sealed record PushCommand(string WorkspacePath) : IRequest<PushResult>;

using MediatR;

namespace Bishop.App.Git;

public sealed record PushCommand(string WorkspacePath) : IRequest<PushResult>;

using Bishop.App.Git;
using MediatR;

namespace Bishop.App.Git.Push;

public sealed class PushCommandHandler : IRequestHandler<PushCommand, PushResult>
{
    private readonly IGitCli _git;

    public PushCommandHandler(IGitCli git) => _git = git;

    public Task<PushResult> Handle(PushCommand request, CancellationToken cancellationToken)
        => _git.PushAsync(request.WorkspacePath, cancellationToken);
}

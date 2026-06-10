using Bishop.App.Git;
using MediatR;

namespace Bishop.App.Git.Push;

internal sealed class PushCommandHandler : IRequestHandler<PushCommand, PushResult>
{
    private readonly IGitCli _git;

    public PushCommandHandler(IGitCli git) => _git = git;

    public Task<PushResult> Handle(PushCommand request, CancellationToken cancellationToken)
        => request.SetUpstream
            ? _git.PushWithSetUpstreamAsync(request.WorkspacePath, cancellationToken)
            : _git.PushAsync(request.WorkspacePath, cancellationToken);
}

using MediatR;

namespace Bishop.App.Git.GetCommitCountSince;

internal sealed class GetCommitCountSinceQueryHandler : IRequestHandler<GetCommitCountSinceQuery, int?>
{
    private readonly IGitCli _git;

    public GetCommitCountSinceQueryHandler(IGitCli git) => _git = git;

    public Task<int?> Handle(GetCommitCountSinceQuery request, CancellationToken cancellationToken)
        => _git.GetCommitCountSinceAsync(request.GitSha, request.WorkspacePath, cancellationToken);
}

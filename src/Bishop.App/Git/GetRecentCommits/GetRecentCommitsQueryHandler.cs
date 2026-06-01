using Bishop.App.Git;
using MediatR;

namespace Bishop.App.Git.GetRecentCommits;

internal sealed class GetRecentCommitsQueryHandler : IRequestHandler<GetRecentCommitsQuery, GetRecentCommitsResult>
{
    private readonly IGitCli _git;

    public GetRecentCommitsQueryHandler(IGitCli git) => _git = git;

    public Task<GetRecentCommitsResult> Handle(GetRecentCommitsQuery request, CancellationToken cancellationToken)
        => _git.GetRecentCommitsAsync(request.WorkspacePath, cancellationToken);
}

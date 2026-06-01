using MediatR;

namespace Bishop.App.Git.GetCurrentBranch;

internal sealed class GetCurrentBranchQueryHandler : IRequestHandler<GetCurrentBranchQuery, string>
{
    private readonly IGitCli _git;

    public GetCurrentBranchQueryHandler(IGitCli git)
    {
        _git = git;
    }

    public Task<string> Handle(GetCurrentBranchQuery request, CancellationToken cancellationToken)
        => _git.GetCurrentBranchAsync(request.WorkspacePath, cancellationToken);
}

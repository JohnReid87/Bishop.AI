using MediatR;

namespace Bishop.App.Git;

public sealed class GetCardCommitQueryHandler : IRequestHandler<GetCardCommitQuery, GetCardCommitResult>
{
    private readonly IGitCli _git;

    public GetCardCommitQueryHandler(IGitCli git) => _git = git;

    public Task<GetCardCommitResult> Handle(GetCardCommitQuery request, CancellationToken cancellationToken)
        => _git.GetCardCommitAsync(request.CardNumber, request.WorkspacePath, cancellationToken);
}

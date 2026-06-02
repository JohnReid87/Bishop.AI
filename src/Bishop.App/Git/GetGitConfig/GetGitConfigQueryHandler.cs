using MediatR;

namespace Bishop.App.Git.GetGitConfig;

internal sealed class GetGitConfigQueryHandler : IRequestHandler<GetGitConfigQuery, GetGitConfigResult>
{
    private readonly IGitCli _git;

    public GetGitConfigQueryHandler(IGitCli git) => _git = git;

    public Task<GetGitConfigResult> Handle(GetGitConfigQuery request, CancellationToken cancellationToken)
        => _git.GetGitConfigAsync(request.WorkspacePath, cancellationToken);
}

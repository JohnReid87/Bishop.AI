using Bishop.App.Git.GetCurrentBranch;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using Bishop.ViewModels.Git;
using MediatR;

namespace Bishop.ViewModels.Workspaces;

internal sealed class BoardGitCoordinator
{
    private readonly ISender _mediator;

    public BoardGitCoordinator(ISender mediator)
    {
        _mediator = mediator;
    }

    public async Task<RecentCommitsResult> GetRecentCommitsAsync(string workspacePath)
    {
        var result = await _mediator.Send(new GetRecentCommitsQuery(workspacePath));
        return Map(result);
    }

    public Task<string> GetCurrentBranchAsync(string workspacePath)
        => _mediator.Send(new GetCurrentBranchQuery(workspacePath));

    public async Task<PushOutcome> PushAsync(string workspacePath, bool setUpstream)
    {
        var result = await _mediator.Send(new PushCommand(workspacePath, SetUpstream: setUpstream));
        return new PushOutcome(result.Success, result.Message);
    }

    private static RecentCommitsResult Map(GetRecentCommitsResult result) => result switch
    {
        GetRecentCommitsResult.Success s => MapSuccess(s),
        GetRecentCommitsResult.NotAGitRepo => new RecentCommitsResult.NotAGitRepo(),
        GetRecentCommitsResult.GitNotFound => new RecentCommitsResult.GitNotFound(),
        GetRecentCommitsResult.NoCommits => new RecentCommitsResult.NoCommits(),
        _ => throw new InvalidOperationException($"Unknown GetRecentCommitsResult: {result.GetType().Name}"),
    };

    private static RecentCommitsResult.Success MapSuccess(GetRecentCommitsResult.Success s)
    {
        var commits = s.Commits
            .Select(c => new CommitItem(c.ShortHash, c.FullHash, c.Subject, c.Body, c.Timestamp, c.IsPushed))
            .ToList();
        return new RecentCommitsResult.Success(commits, s.UpstreamRef, s.UpstreamIsTracked, s.UnpushedCount);
    }
}

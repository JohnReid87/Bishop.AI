using Bishop.App.Git;

namespace Bishop.App.Git.GetRecentCommits;

public abstract record GetRecentCommitsResult
{
    public sealed record Success(IReadOnlyList<CommitInfo> Commits, string? UpstreamRef) : GetRecentCommitsResult;
    public sealed record NotAGitRepo : GetRecentCommitsResult;
    public sealed record GitNotFound : GetRecentCommitsResult;
    public sealed record NoCommits : GetRecentCommitsResult;
}

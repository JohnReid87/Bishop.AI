namespace Bishop.App.Git;

public abstract record GetRecentCommitsResult
{
    public sealed record Success(IReadOnlyList<CommitInfo> Commits) : GetRecentCommitsResult;
    public sealed record NotAGitRepo : GetRecentCommitsResult;
    public sealed record GitNotFound : GetRecentCommitsResult;
    public sealed record NoCommits : GetRecentCommitsResult;
}

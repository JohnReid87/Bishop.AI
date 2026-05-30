namespace Bishop.ViewModels.GitHub;

public abstract record RecentCommitsResult
{
    public sealed record Success(
        IReadOnlyList<CommitItem> Commits,
        string? UpstreamRef,
        bool UpstreamIsTracked,
        int UnpushedCount) : RecentCommitsResult;

    public sealed record NotAGitRepo : RecentCommitsResult;

    public sealed record GitNotFound : RecentCommitsResult;

    public sealed record NoCommits : RecentCommitsResult;
}

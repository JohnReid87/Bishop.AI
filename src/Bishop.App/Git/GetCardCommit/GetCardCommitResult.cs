using Bishop.App.Git;

namespace Bishop.App.Git.GetCardCommit;

public abstract record GetCardCommitResult
{
    public sealed record Found(CommitInfo Commit) : GetCardCommitResult;
    public sealed record NotFound : GetCardCommitResult;
    public sealed record NotAGitRepo : GetCardCommitResult;
    public sealed record GitNotFound : GetCardCommitResult;
}

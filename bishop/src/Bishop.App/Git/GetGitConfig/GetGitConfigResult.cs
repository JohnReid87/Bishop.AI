namespace Bishop.App.Git.GetGitConfig;

public enum GitIdentityScope
{
    Repo,
    Global,
    Unset,
}

public abstract record GetGitConfigResult
{
    public sealed record Success(
        string? OriginUrl,
        string Branch,
        string? UpstreamRef,
        bool UpstreamIsTracked,
        int Ahead,
        int Behind,
        int StagedCount,
        int UnstagedCount,
        string? Name,
        string? Email,
        GitIdentityScope IdentityScope) : GetGitConfigResult;

    public sealed record NotAGitRepo : GetGitConfigResult;
    public sealed record GitNotFound : GetGitConfigResult;
}

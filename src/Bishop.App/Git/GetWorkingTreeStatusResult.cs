namespace Bishop.App.Git;

public abstract record GetWorkingTreeStatusResult
{
    public sealed record Clean : GetWorkingTreeStatusResult;
    public sealed record Dirty(IReadOnlyList<string> Paths) : GetWorkingTreeStatusResult;
    public sealed record NotAGitRepo : GetWorkingTreeStatusResult;
    public sealed record GitNotFound : GetWorkingTreeStatusResult;
}

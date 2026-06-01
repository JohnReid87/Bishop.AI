using Bishop.App.Git;

namespace Bishop.ViewModels.Cards;

internal readonly record struct CardCommitState(
    string? ShortHash,
    string? Url,
    bool IsLinkVisible,
    bool IsTextVisible)
{
    internal static readonly CardCommitState None = new(null, null, false, false);

    internal static CardCommitState From(CommitInfo commit, string? gitHubRepo)
    {
        var url = commit.IsPushed && gitHubRepo is not null
            ? $"https://github.com/{gitHubRepo}/commit/{commit.FullHash}"
            : null;
        return new(
            commit.ShortHash,
            url,
            IsLinkVisible: url is not null,
            IsTextVisible: commit.ShortHash is not null && url is null);
    }
}

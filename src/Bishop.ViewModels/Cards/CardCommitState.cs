using Bishop.App.Git;

namespace Bishop.ViewModels.Cards;

internal readonly record struct CardCommitState(string? ShortHash)
{
    internal static readonly CardCommitState None = new((string?)null);

    internal static CardCommitState From(CommitInfo commit) => new(commit.ShortHash);
}

using Bishop.Core;

namespace Bishop.App.Cards.ImportFromGitHub;

public sealed record ImportFromGitHubResult(
    IReadOnlyList<Card> Imported,
    IReadOnlyList<int> SkippedAlreadyPresent,
    IReadOnlyList<ImportFailure> Failed);

public sealed record ImportFailure(int IssueNumber, string Error);

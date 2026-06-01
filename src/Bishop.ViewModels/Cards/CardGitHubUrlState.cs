namespace Bishop.ViewModels.Cards;

internal readonly record struct CardGitHubUrlState(string? IssueUrl, bool CanPush)
{
    internal static CardGitHubUrlState For(int? issueNumber, string? gitHubRepo) =>
        new(issueNumber is not null && gitHubRepo is not null
                ? $"https://github.com/{gitHubRepo}/issues/{issueNumber}"
                : null,
            CanPush: issueNumber is null && gitHubRepo is not null);
}

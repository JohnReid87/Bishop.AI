using Bishop.Core;

namespace Bishop.ViewModels;

public sealed class BatchItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public BatchStatus Status { get; init; }
    public int CardCount { get; init; }
    public string? GitHubPrUrl { get; init; }

    public string StatusLabel => Status switch
    {
        BatchStatus.Working => "Working",
        BatchStatus.Closed => "Closed",
        _ => "Open"
    };
    public string CardCountLabel => CardCount == 1 ? "1 card" : $"{CardCount} cards";

    public bool CanRun => Status == BatchStatus.Open;
    public bool CanFinish => Status == BatchStatus.Working && !HasGitHubPr;
    public bool CanComplete => Status == BatchStatus.Working && HasGitHubPr;
    public bool CanAbandon => Status == BatchStatus.Working;
    public bool HasGitHubPr => !string.IsNullOrEmpty(GitHubPrUrl);
}

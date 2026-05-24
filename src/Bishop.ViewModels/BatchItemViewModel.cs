using Bishop.Core;

namespace Bishop.ViewModels;

public sealed class BatchItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public BatchStatus Status { get; init; }
    public int CardCount { get; init; }

    public string StatusLabel => Status == BatchStatus.Working ? "Working" : "Open";
    public string CardCountLabel => CardCount == 1 ? "1 card" : $"{CardCount} cards";

    public bool CanRun => Status == BatchStatus.Open;
    public bool CanFinish => Status == BatchStatus.Working;
    public bool CanAbandon => Status == BatchStatus.Working;
}

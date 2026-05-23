using Bishop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class PushLaneToGitHubDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    public ObservableCollection<string> WillPushItems { get; } = [];

    public int SkippedCount { get; }

    public bool HasWillPush => WillPushItems.Count > 0;

    public string PreviewSummary { get; }

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial bool WasPushed { get; set; }

    public PushLaneToGitHubDialogViewModel(IReadOnlyList<CardViewModel> cards)
    {
        foreach (var card in cards)
        {
            if (card.GitHubIssueNumber is null)
                WillPushItems.Add($"#{card.Number} {card.Title}");
            else
                SkippedCount++;
        }
        PreviewSummary = $"{WillPushItems.Count} to push, {SkippedCount} already linked";
    }

    public void ApplyResult(int pushed, int skipped, int failed)
    {
        var summary = $"{pushed} pushed, {skipped} skipped";
        if (failed > 0) summary += $", {failed} failed";
        ResultSummary = summary;
        HasResults = true;
        WasPushed = pushed > 0;
    }

    public void ApplyError(string message)
    {
        ResultSummary = $"Push failed: {message}";
        HasResults = true;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bishop.ViewModels;

public sealed partial class SkillRunRowViewModel : ObservableObject
{
    public string SkillName { get; }
    public string LastRunText { get; }
    public string CommitsSinceText { get; }
    public string StatusDotColor { get; }
    public string StatusTooltip { get; }
    public int SeverityRank { get; }

    [ObservableProperty]
    private string _selectedModelId = "claude-sonnet-4-6";

    [ObservableProperty]
    private string _selectedModelLabel = "Sonnet 4.6 ▾";

    [ObservableProperty]
    private string _modelSelectionReason = string.Empty;

    [RelayCommand]
    private void SelectModel(string modelId)
    {
        SelectedModelId = modelId;
        var label = WorkNextOptionsDialogViewModel.Models.FirstOrDefault(m => m.Id == modelId)?.Label ?? "Sonnet 4.6";
        SelectedModelLabel = $"{label} ▾";
    }

    public SkillRunRowViewModel(string skillName, DateTimeOffset? lastRun, int? commitsSince, bool shaUnreachable)
    {
        SkillName = skillName;
        LastRunText = lastRun is null ? "Never" : FormatRelativeTime(lastRun.Value);

        if (lastRun is null)
        {
            CommitsSinceText = "—";
            StatusDotColor = "#c97a8a";
            StatusTooltip = "Never audited";
            SeverityRank = 2;
        }
        else if (shaUnreachable)
        {
            CommitsSinceText = "Re-audit";
            StatusDotColor = "#c97a8a";
            StatusTooltip = "Audit SHA is no longer reachable from HEAD";
            SeverityRank = 2;
        }
        else
        {
            var count = commitsSince ?? 0;
            CommitsSinceText = count.ToString();
            StatusDotColor = count < 10 ? "#4a9e6a" : count < 50 ? "#c4a85f" : "#c97a8a";
            StatusTooltip = count < 10 ? "Fresh" : count < 50 ? "Getting stale" : "Stale — re-audit recommended";
            SeverityRank = count < 10 ? 0 : count < 50 ? 1 : 2;
        }
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }
}

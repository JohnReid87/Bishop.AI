using Bishop.App.Skills;
using Bishop.ViewModels.Findings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace Bishop.ViewModels.Skills;

public sealed partial class SkillRunRowViewModel : ObservableObject
{
    public string SkillName { get; }
    public string? ProjectName { get; }
    public string DisplayLabel => string.IsNullOrEmpty(ProjectName) ? SkillName : $"{SkillName} · {ProjectName}";
    public string LastRunText { get; }
    public string CommitsSinceText { get; }
    public string StatusDotColor { get; }
    public string StatusTooltip { get; }
    public int SeverityRank { get; }
    public string? ReportFilePath { get; }
    public int? FindingsCount { get; }
    public bool FindingsBadgeIsVisible => FindingsCount.HasValue;

    public Guid WorkspaceId { get; }
    public string WorkspacePath { get; }
    public string? GitHubRepo { get; }

    public bool CanViewFindings =>
        FindingsCount is > 0
        && !SkillName.Equals("bish-coverage", StringComparison.OrdinalIgnoreCase);

    public event Action<FindingsPageNavArgs>? ViewFindingsRequested;

    [ObservableProperty]
    private string _selectedModelId = ClaudeModels.Sonnet46;

    [ObservableProperty]
    private string _selectedModelLabel = ClaudeModels.Sonnet46Display + " ▾";

    [ObservableProperty]
    private string _modelSelectionReason = string.Empty;

    [RelayCommand]
    private void SelectModel(string modelId)
    {
        SelectedModelId = modelId;
        var label = SkillModels.All.FirstOrDefault(m => m.Id == modelId)?.Label ?? ClaudeModels.Sonnet46Display;
        SelectedModelLabel = $"{label} ▾";
    }

    [RelayCommand]
    private void ViewFindings()
    {
        if (!CanViewFindings) return;
        ViewFindingsRequested?.Invoke(new FindingsPageNavArgs(
            WorkspaceId, WorkspacePath, GitHubRepo, SkillName, ProjectName));
    }

    public SkillRunRowViewModel(
        string skillName,
        DateTimeOffset? lastRun,
        int? commitsSince,
        bool shaUnreachable,
        string workspacePath = "",
        int? findingsCount = null,
        TimeProvider? timeProvider = null,
        string? projectName = null,
        Guid workspaceId = default,
        string? gitHubRepo = null)
    {
        SkillName = skillName;
        ProjectName = projectName;
        WorkspaceId = workspaceId;
        WorkspacePath = workspacePath;
        GitHubRepo = gitHubRepo;
        ReportFilePath = ResolveReportFilePath(skillName, workspacePath);
        FindingsCount = findingsCount;
        LastRunText = lastRun is null ? "Never" : FormatRelativeTime(lastRun.Value, timeProvider ?? TimeProvider.System);

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

    private static string? ResolveReportFilePath(string skillName, string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
            return null;
        if (skillName.Equals("bish-coverage", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(workspacePath, "TestResults", "coverage-report", "index.html");
        return null;
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp, TimeProvider timeProvider)
    {
        var elapsed = timeProvider.GetUtcNow() - timestamp.ToUniversalTime();
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }
}

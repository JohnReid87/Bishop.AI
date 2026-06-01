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

    public bool CanViewFindings => FindingsCount is > 0;
    public bool CanViewReport => ReportFilePath is not null;

    public string FindingsButtonText => $"View ({FindingsCount ?? 0})";

    public event Action<FindingsPageNavArgs>? ViewFindingsRequested;
    public event Action<Uri>? ViewReportRequested;

    [ObservableProperty]
    private string _selectedModelId = ClaudeModels.Sonnet46;

    [ObservableProperty]
    private string _selectedModelLabel = ClaudeModels.Sonnet46Display + " ▾";

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

    [RelayCommand]
    private void ViewReport()
    {
        if (ReportFilePath is null) return;
        ViewReportRequested?.Invoke(new Uri(ReportFilePath));
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
        LastRunText = lastRun is null ? "Never" : RelativeTimeFormatter.Format(lastRun.Value, timeProvider ?? TimeProvider.System);

        var status = SkillRunStatus.For(lastRun, commitsSince, shaUnreachable);
        CommitsSinceText = status.CommitsSince;
        StatusDotColor = status.DotColor;
        StatusTooltip = status.Tooltip;
        SeverityRank = status.SeverityRank;
    }

    private static string? ResolveReportFilePath(string skillName, string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
            return null;
        if (skillName.Equals("bish-coverage", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(workspacePath, "TestResults", "coverage-report", "index.html");
        return null;
    }
}

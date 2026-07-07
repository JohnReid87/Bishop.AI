using Bishop.App.Git.GetCommitCountSince;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.Core.Skills;
using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceMonitoringViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly TimeProvider _timeProvider;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;

    public event Action<FindingsPageNavArgs>? ViewFindingsRequested;
    public event Action<Uri>? ViewReportRequested;

    public ObservableCollection<SkillRunRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private int _badgeCount;

    [ObservableProperty]
    private string _badgeColor = string.Empty;

    [ObservableProperty]
    private bool _badgeIsVisible;

    [ObservableProperty]
    private string _badgeTooltip = string.Empty;

    [ObservableProperty]
    private SkillRunRowViewModel? _selectedRow;

    public WorkspaceMonitoringViewModel(ISender mediator, TimeProvider timeProvider)
    {
        _mediator = mediator;
        _timeProvider = timeProvider;
    }

    public async Task LoadAsync(Guid workspaceId, string workspacePath)
    {
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var runs = await _mediator.Send(new GetWorkspaceSkillRunsQuery(_workspaceId));
        var runsBySkill = runs
            .GroupBy(r => r.SkillName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var trackedSkills = await GetTrackedSkillsAsync();

        var tasks = new List<Task<SkillRunRowViewModel>>();
        foreach (var skillName in trackedSkills)
        {
            if (runsBySkill.TryGetValue(skillName, out var skillRuns) && skillRuns.Count > 0)
            {
                foreach (var run in skillRuns)
                    tasks.Add(BuildRowAsync(skillName, run));
            }
            else
            {
                tasks.Add(BuildRowAsync(skillName, run: null));
            }
        }

        var rows = await Task.WhenAll(tasks);

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);

        UpdateBadge();
    }

    // The Monitoring view tracks every installed skill in a review/analysis category
    // (Code / Tests / Review) — the same categories the board launcher hides — so those
    // skills have exactly one UI home. Ordered by category then name for a stable layout.
    private async Task<IReadOnlyList<string>> GetTrackedSkillsAsync()
    {
        var installed = await _mediator.Send(new DiscoverSkillsQuery());
        return installed
            .Where(s => s.Category.IsMonitored())
            .OrderBy(s => (int)s.Category)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.Name)
            .ToArray();
    }

    private async Task<SkillRunRowViewModel> BuildRowAsync(string skillName, Bishop.Core.WorkspaceSkillRun? run)
    {
        int? commitsSince = null;
        var shaUnreachable = false;

        if (run is not null && !string.IsNullOrEmpty(run.GitSha))
        {
            commitsSince = await _mediator.Send(new GetCommitCountSinceQuery(run.GitSha, _workspacePath));
            shaUnreachable = commitsSince is null;
        }

        var row = new SkillRunRowViewModel(
            skillName,
            run?.RecordedAt,
            commitsSince,
            shaUnreachable,
            _workspacePath,
            run?.FindingsCount,
            _timeProvider,
            run?.ProjectName,
            _workspaceId);
        row.ViewFindingsRequested += args => ViewFindingsRequested?.Invoke(args);
        row.ViewReportRequested += uri => ViewReportRequested?.Invoke(uri);
        return row;
    }

    private void UpdateBadge()
    {
        var attentionCount = Rows.Count(r => r.SeverityRank > 0);
        var hasRed = Rows.Any(r => r.SeverityRank >= 2);

        BadgeCount = attentionCount;
        BadgeColor = hasRed ? "#c97a8a" : attentionCount > 0 ? "#c4a85f" : string.Empty;
        BadgeIsVisible = attentionCount > 0;
        BadgeTooltip = attentionCount > 0
            ? $"{attentionCount} of {Rows.Count} reviews need attention"
            : string.Empty;
    }

}

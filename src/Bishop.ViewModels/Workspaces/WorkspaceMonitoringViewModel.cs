using Bishop.App.Git;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceMonitoringViewModel : ObservableObject
{
    private static readonly string[] TrackedSkills =
    [
        "bish-arch",
        "bish-tests",
        "bish-coverage",
        "bish-security",
        "bish-dead-code",
    ];

    private readonly ISender _mediator;
    private readonly IGitCli _gitCli;
    private readonly TimeProvider _timeProvider;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;
    private string? _gitHubRepo;

    public event Action<FindingsPageNavArgs>? ViewFindingsRequested;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedReport))]
    private Uri? _selectedReportUri;

    public bool HasSelectedReport => SelectedReportUri is not null;

    partial void OnSelectedRowChanged(SkillRunRowViewModel? value)
    {
        SelectedReportUri = value?.ReportFilePath is { } path
            ? new Uri(path)
            : null;
    }

    public WorkspaceMonitoringViewModel(ISender mediator, IGitCli gitCli, TimeProvider timeProvider)
    {
        _mediator = mediator;
        _gitCli = gitCli;
        _timeProvider = timeProvider;
    }

    public async Task LoadAsync(Guid workspaceId, string workspacePath, string? gitHubRepo = null)
    {
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        _gitHubRepo = gitHubRepo;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var runs = await _mediator.Send(new GetWorkspaceSkillRunsQuery(_workspaceId));
        var runsBySkill = runs
            .GroupBy(r => r.SkillName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<SkillRunRowViewModel>();
        foreach (var skillName in TrackedSkills)
        {
            if (runsBySkill.TryGetValue(skillName, out var skillRuns) && skillRuns.Count > 0)
            {
                foreach (var run in skillRuns)
                    rows.Add(await BuildRowAsync(skillName, run));
            }
            else
            {
                rows.Add(await BuildRowAsync(skillName, run: null));
            }
        }

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);

        UpdateBadge();
    }

    private async Task<SkillRunRowViewModel> BuildRowAsync(string skillName, Bishop.Core.WorkspaceSkillRun? run)
    {
        int? commitsSince = null;
        var shaUnreachable = false;

        if (run is not null && !string.IsNullOrEmpty(run.GitSha))
        {
            commitsSince = await _gitCli.GetCommitCountSinceAsync(run.GitSha, _workspacePath);
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
            _workspaceId,
            _gitHubRepo);
        row.ViewFindingsRequested += args => ViewFindingsRequested?.Invoke(args);
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

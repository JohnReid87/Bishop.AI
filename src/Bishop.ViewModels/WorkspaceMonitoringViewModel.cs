using Bishop.App.Git;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class WorkspaceMonitoringViewModel : ObservableObject
{
    private static readonly string[] TrackedSkills =
    [
        "bish-audit-docs",
        "bish-arch",
        "bish-tests",
        "bish-coverage",
        "bish-security",
    ];

    private readonly ISender _mediator;
    private readonly IGitCli _gitCli;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;

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

    public WorkspaceMonitoringViewModel(ISender mediator, IGitCli gitCli)
    {
        _mediator = mediator;
        _gitCli = gitCli;
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
        var runsBySkill = runs.ToDictionary(r => r.SkillName, StringComparer.OrdinalIgnoreCase);

        var skills = await _mediator.Send(new DiscoverSkillsQuery());
        var skillsByName = skills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SkillRunRowViewModel>();
        foreach (var skillName in TrackedSkills)
        {
            runsBySkill.TryGetValue(skillName, out var run);
            int? commitsSince = null;
            var shaUnreachable = false;

            if (run is not null && !string.IsNullOrEmpty(run.GitSha))
            {
                commitsSince = await _gitCli.GetCommitCountSinceAsync(run.GitSha, _workspacePath);
                shaUnreachable = commitsSince is null;
            }

            skillsByName.TryGetValue(skillName, out var skill);
            var isFirstRun = run is null;
            var defaultModelId = isFirstRun
                ? SkillModelOptions.ResolveModelId(skill?.FirstRunModel)
                : SkillModelOptions.ResolveModelId(skill?.ReRunModel);

            var row = new SkillRunRowViewModel(skillName, run?.RecordedAt, commitsSince, shaUnreachable, _workspacePath);
            row.SelectModelCommand.Execute(defaultModelId);
            row.ModelSelectionReason = isFirstRun ? "(first run)" : "(re-run default)";
            rows.Add(row);
        }

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);

        UpdateBadge();
    }

    private void UpdateBadge()
    {
        var attentionCount = Rows.Count(r => r.SeverityRank > 0);
        var hasRed = Rows.Any(r => r.SeverityRank >= 2);

        BadgeCount = attentionCount;
        BadgeColor = hasRed ? "#c97a8a" : attentionCount > 0 ? "#c4a85f" : string.Empty;
        BadgeIsVisible = attentionCount > 0;
        BadgeTooltip = attentionCount > 0
            ? $"{attentionCount} of {TrackedSkills.Length} reviews need attention"
            : string.Empty;
    }

}

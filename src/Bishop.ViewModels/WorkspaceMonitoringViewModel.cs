using Bishop.App.Git;
using Bishop.App.Services.Settings;
using Bishop.App.Skills;
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

    private readonly IMediator _mediator;
    private readonly IGitCli _gitCli;
    private readonly IAppSettings _appSettings;
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

    public WorkspaceMonitoringViewModel(IMediator mediator, IGitCli gitCli, IAppSettings appSettings)
    {
        _mediator = mediator;
        _gitCli = gitCli;
        _appSettings = appSettings;
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

            var savedModelId = SkillModelOptions.ResolveModelId(await _appSettings.GetAsync($"skill.{skillName}.last_model"));
            var row = new SkillRunRowViewModel(skillName, run?.RecordedAt, commitsSince, shaUnreachable);
            row.SelectModelCommand.Execute(savedModelId);
            row.PropertyChanged += async (sender, args) =>
            {
                if (args.PropertyName == nameof(SkillRunRowViewModel.SelectedModelId) && sender is SkillRunRowViewModel r)
                    await _appSettings.SetAsync($"skill.{r.SkillName}.last_model", r.SelectedModelId);
            };
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
        BadgeColor = hasRed ? "#ff5555" : attentionCount > 0 ? "#c4944f" : string.Empty;
        BadgeIsVisible = attentionCount > 0;
        BadgeTooltip = attentionCount > 0
            ? $"{attentionCount} of {TrackedSkills.Length} reviews need attention"
            : string.Empty;
    }
}

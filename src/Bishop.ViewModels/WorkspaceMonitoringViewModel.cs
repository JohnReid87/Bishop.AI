using Bishop.App.Git;
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
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;

    public ObservableCollection<SkillRunRowViewModel> Rows { get; } = [];

    public WorkspaceMonitoringViewModel(IMediator mediator, IGitCli gitCli)
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

            rows.Add(new SkillRunRowViewModel(skillName, run?.RecordedAt, commitsSince, shaUnreachable));
        }

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
    }
}

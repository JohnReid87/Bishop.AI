using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Findings;

public sealed partial class FindingsViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly ICardDetailDialogService _dialogService;

    private Guid _workspaceId;
    private string _workspacePath = string.Empty;
    private string? _gitHubRepo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    private string _skillName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    private string? _projectName;

    public string Header => string.IsNullOrEmpty(ProjectName)
        ? $"{SkillName} — findings"
        : $"{SkillName} · {ProjectName} — findings";

    public ObservableCollection<FindingItemViewModel> Findings { get; } = [];

    public ObservableCollection<FindingItemViewModel> ResolvedFindings { get; } = [];

    [ObservableProperty]
    private string _filterText = string.Empty;

    public bool IsEmpty => Findings.Count == 0 && ResolvedFindings.Count == 0;

    public bool HasResolved => ResolvedFindings.Count > 0;

    public FindingsViewModel(ISender mediator, ICardDetailDialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    public async Task LoadAsync(
        Guid workspaceId,
        string workspacePath,
        string? gitHubRepo,
        string skillName,
        string? projectName,
        CancellationToken cancellationToken = default)
    {
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        _gitHubRepo = gitHubRepo;
        SkillName = skillName;
        ProjectName = projectName;

        var records = await _mediator.Send(
            new GetFindingsBySkillAndProjectQuery(workspaceId, skillName, projectName),
            cancellationToken);

        var active = new List<FindingItemViewModel>();
        var resolved = new List<FindingItemViewModel>();
        foreach (var r in records)
        {
            var vm = new FindingItemViewModel(
                r, skillName, workspaceId, workspacePath, gitHubRepo,
                _mediator, _dialogService);
            if (r.Status == "resolved")
                resolved.Add(vm);
            else
                active.Add(vm);
        }

        Findings.Clear();
        foreach (var vm in active
            .OrderBy(SeverityRank)
            .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase))
            Findings.Add(vm);

        ResolvedFindings.Clear();
        foreach (var vm in resolved
            .OrderBy(SeverityRank)
            .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase))
            ResolvedFindings.Add(vm);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasResolved));
    }

    private static int SeverityRank(FindingItemViewModel f) => (f.Severity ?? string.Empty).ToLowerInvariant() switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" or "med" => 2,
        "low" => 3,
        "info" => 4,
        _ => 5,
    };

    public bool Matches(FindingItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        var q = FilterText.Trim();
        return ContainsOic(item.Title, q)
            || ContainsOic(item.Body, q)
            || ContainsOic(item.File, q)
            || ContainsOic(item.Symbol, q)
            || ContainsOic(item.Severity, q);
    }

    private static bool ContainsOic(string? source, string query) =>
        source?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
}

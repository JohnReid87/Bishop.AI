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

    private string _sortKey = "severity";
    private bool _sortAsc = true;

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

    public ObservableCollection<FindingItemViewModel> VisibleFindings { get; } = [];

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

        Findings.Clear();
        ResolvedFindings.Clear();
        foreach (var r in records)
        {
            var vm = new FindingItemViewModel(
                r, skillName, workspaceId, workspacePath, gitHubRepo,
                _mediator, _dialogService);
            if (r.Status == "resolved")
                ResolvedFindings.Add(vm);
            else
                Findings.Add(vm);
        }

        // Resolved list never re-sorts at runtime — sort once here.
        SortInPlace(ResolvedFindings);

        ApplyView();

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasResolved));
    }

    public void ToggleSort(string key)
    {
        if (_sortKey == key) _sortAsc = !_sortAsc;
        else { _sortKey = key; _sortAsc = true; }
        ApplyView();
    }

    partial void OnFilterTextChanged(string value) => ApplyView();

    private void ApplyView()
    {
        IEnumerable<FindingItemViewModel> items = Findings;
        if (!string.IsNullOrWhiteSpace(FilterText))
            items = items.Where(Matches);

        items = _sortKey switch
        {
            "severity" => _sortAsc
                ? items.OrderBy(FindingSeverityRanker.Rank).ThenBy(i => i.Title)
                : items.OrderByDescending(FindingSeverityRanker.Rank).ThenBy(i => i.Title),
            "location" => _sortAsc
                ? items.OrderBy(i => i.Location, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.Location, StringComparer.OrdinalIgnoreCase),
            _ => _sortAsc
                ? items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.Title, StringComparer.OrdinalIgnoreCase),
        };

        VisibleFindings.Clear();
        foreach (var item in items)
            VisibleFindings.Add(item);
    }

    private static void SortInPlace(ObservableCollection<FindingItemViewModel> list)
    {
        var sorted = list
            .OrderBy(FindingSeverityRanker.Rank)
            .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        list.Clear();
        foreach (var item in sorted)
            list.Add(item);
    }

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

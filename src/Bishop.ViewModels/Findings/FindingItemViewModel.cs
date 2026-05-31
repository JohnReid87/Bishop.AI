using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Findings.DismissFinding;
using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.Core;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;

namespace Bishop.ViewModels.Findings;

public sealed partial class FindingItemViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly ICardDetailDialogService _dialogService;
    private readonly ISkillTagMap _skillTagMap;
    private readonly Guid _workspaceId;
    private readonly string _workspacePath;
    private readonly string? _gitHubRepo;
    private readonly string _skillName;

    public Guid Id { get; }
    public string Title { get; }
    public string Body { get; }
    public string? Severity { get; }
    public string? File { get; }
    public string? Symbol { get; }
    public string? Rule { get; }

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private int? _linkedCardId;

    [ObservableProperty]
    private string? _rebuttalText;

    public FindingItemViewModel(
        FindingRecord record,
        string skillName,
        Guid workspaceId,
        string workspacePath,
        string? gitHubRepo,
        ISender mediator,
        ICardDetailDialogService dialogService,
        ISkillTagMap skillTagMap)
    {
        Id = record.Id;
        Title = record.Title;
        Body = record.Body;
        Severity = record.Severity;
        File = record.File;
        Symbol = record.Symbol;
        Rule = record.Rule;
        _status = record.Status;
        _linkedCardId = record.LinkedCardId;
        _rebuttalText = record.RebuttalText;

        _skillName = skillName;
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        _gitHubRepo = gitHubRepo;
        _mediator = mediator;
        _dialogService = dialogService;
        _skillTagMap = skillTagMap;
    }

    public string Location => string.IsNullOrEmpty(File)
        ? Symbol ?? string.Empty
        : string.IsNullOrEmpty(Symbol) ? File : $"{File} · {Symbol}";

    public string SeverityColor => (Severity ?? string.Empty).ToLowerInvariant() switch
    {
        "critical" or "high" => "#c97a8a",
        "medium" or "med" => "#c4a85f",
        "low" or "info" => "#5fa89c",
        _ => "#9aa86a",
    };

    public bool HasSeverity => !string.IsNullOrEmpty(Severity);

    public string StatusLabel => Status switch
    {
        "dismissed" => "dismissed",
        "parked" => "parked",
        "resolved" => "resolved",
        _ when LinkedCardId is { } n => $"#{n}",
        _ => "pending",
    };

    public bool IsResolved => Status == "resolved";
    public bool IsDismissed => Status == "dismissed";
    public bool HasRebuttal => !string.IsNullOrWhiteSpace(RebuttalText);

    public bool IsConvertToCardEnabled =>
        LinkedCardId is null && Status != "dismissed" && Status != "resolved";

    public bool IsDismissEnabled =>
        Status != "dismissed" && Status != "resolved" && LinkedCardId is null;

    [RelayCommand]
    private async Task ConvertToCardAsync(object? xamlRoot)
    {
        if (xamlRoot is null) return;
        if (!IsConvertToCardEnabled) return;

        var tag = _skillTagMap.GetTag(_skillName);
        var description = BuildDescription();

        var card = await _mediator.Send(new AddCardCommand(
            _workspaceId,
            SystemLaneNames.ToDo,
            Title,
            description,
            tag,
            CardInsertPosition.Top));

        var cardVm = new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = card.LaneName,
            TagName = card.TagName,
            IsClosed = card.IsClosed,
            GitHubIssueNumber = card.GitHubIssueNumber,
        };

        var saved = await _dialogService.ShowAsync(cardVm, _workspacePath, _workspaceId, _gitHubRepo, xamlRoot);
        if (!saved)
        {
            await _mediator.Send(new RemoveCardCommand(card.Id));
            return;
        }

        LinkedCardId = card.Number;
        Status = $"carded:#{card.Number}";
        NotifyDerived();
    }

    [RelayCommand]
    private async Task DismissAsync(string? rebuttal)
    {
        if (!IsDismissEnabled) return;
        if (string.IsNullOrWhiteSpace(rebuttal)) return;

        await _mediator.Send(new DismissFindingCommand(Id, rebuttal.Trim()));

        Status = "dismissed";
        RebuttalText = rebuttal.Trim();
        NotifyDerived();
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsConvertToCardEnabled));
        OnPropertyChanged(nameof(IsDismissEnabled));
        OnPropertyChanged(nameof(IsResolved));
        OnPropertyChanged(nameof(IsDismissed));
        OnPropertyChanged(nameof(HasRebuttal));
    }

    private string BuildDescription()
    {
        var location = string.IsNullOrEmpty(Location) ? "(unknown)" : Location;
        return $"### Why\n{Body}\n\n### Related\nFrom `{_skillName}` review at `{location}`";
    }
}

using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Findings.DismissFinding;
using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.App.Findings.LinkFindingToCard;
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
    private readonly Guid _workspaceId;
    private readonly string _workspacePath;
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
    private bool? _linkedCardIsClosed;

    [ObservableProperty]
    private string? _rebuttalText;

    [ObservableProperty]
    private string _rebuttalDraft = string.Empty;

    internal FindingItemViewModel(
        FindingRecord record,
        string skillName,
        Guid workspaceId,
        string workspacePath,
        ISender mediator,
        ICardDetailDialogService dialogService)
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
        _linkedCardIsClosed = record.LinkedCardIsClosed;
        _rebuttalText = record.RebuttalText;

        _skillName = skillName;
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        _mediator = mediator;
        _dialogService = dialogService;
    }

    public string Location => string.IsNullOrEmpty(File)
        ? Symbol ?? string.Empty
        : string.IsNullOrEmpty(Symbol) ? File : $"{File} · {Symbol}";

    public string SeverityColor => FindingSeverityColor.For(Severity);

    public bool HasSeverity => !string.IsNullOrEmpty(Severity);

    public string StatusLabel => FindingStatusState.For(Status, LinkedCardId, LinkedCardIsClosed).StatusLabel;

    public bool IsResolved => Status == "resolved";
    public bool IsDismissed => Status == "dismissed";
    public bool HasRebuttal => !string.IsNullOrWhiteSpace(RebuttalText);
    public bool HasLinkedCard => LinkedCardId is not null && LinkedCardIsClosed is not null;
    public string LinkedCardLabel => LinkedCardId is { } n ? $"Open card #{n}" : string.Empty;

    public bool IsConvertToCardVisible =>
        FindingStatusState.For(Status, LinkedCardId, LinkedCardIsClosed).IsConvertToCardVisible;

    public bool IsDismissEnabled =>
        FindingStatusState.For(Status, LinkedCardId, LinkedCardIsClosed).IsDismissEnabled;

    [RelayCommand]
    private async Task ConvertToCardAsync(object? xamlRoot)
    {
        if (xamlRoot is null) return;
        if (!IsConvertToCardVisible) return;

        var tag = SkillTagMap.GetTag(_skillName);
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
        };

        var saved = await _dialogService.ShowAsync(cardVm, _workspacePath, _workspaceId, xamlRoot);
        if (!saved)
        {
            await _mediator.Send(new RemoveCardCommand(card.Id));
            return;
        }

        await _mediator.Send(new LinkFindingToCardCommand(Id, card.Number));

        LinkedCardId = card.Number;
        LinkedCardIsClosed = card.IsClosed;
        Status = "carded";
        NotifyDerived();
    }

    [RelayCommand]
    private async Task OpenLinkedCardAsync(object? xamlRoot)
    {
        if (xamlRoot is null) return;
        if (LinkedCardId is not { } number) return;

        var card = await _mediator.Send(new GetCardByNumberQuery(number, _workspaceId));
        if (card is null)
        {
            await _dialogService.ShowNotFoundAsync(number, xamlRoot);
            return;
        }

        var cardVm = new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = card.LaneName,
            TagName = card.TagName,
            IsClosed = card.IsClosed,
        };

        await _dialogService.ShowAsync(cardVm, _workspacePath, _workspaceId, xamlRoot);
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
        OnPropertyChanged(nameof(IsConvertToCardVisible));
        OnPropertyChanged(nameof(IsDismissEnabled));
        OnPropertyChanged(nameof(IsResolved));
        OnPropertyChanged(nameof(IsDismissed));
        OnPropertyChanged(nameof(HasRebuttal));
        OnPropertyChanged(nameof(HasLinkedCard));
        OnPropertyChanged(nameof(LinkedCardLabel));
    }

    private string BuildDescription()
    {
        var location = string.IsNullOrEmpty(Location) ? "(unknown)" : Location;
        return $"### Why\n{Body}\n\n### Related\nFrom `{_skillName}` review at `{location}`";
    }
}

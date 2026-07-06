using Bishop.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels.Cards;

public sealed partial class CardViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LaneName { get; init; } = string.Empty;
    public string? TagName { get; init; }
    public string? TagColour { get; init; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardOpacity))]
    [NotifyPropertyChangedFor(nameof(CloseReopenGlyph))]
    [NotifyPropertyChangedFor(nameof(CloseReopenTooltip))]
    private bool _isClosed;
    public Guid? BatchId { get; init; }
    public string? BatchName { get; init; }
    public DateTimeOffset? BatchCreatedAt { get; init; }
    public BatchStatus? BatchStatus { get; init; }
    public DateTimeOffset? BatchFinishedAt { get; init; }
    public DateTimeOffset? BatchMergedAt { get; init; }
    public DateTimeOffset? BatchStoppedAt { get; init; }

    [ObservableProperty]
    private bool _isSelected;
    public DateTimeOffset? LastAutoRunFailedAt { get; init; }
    public DateTimeOffset? LastAutoRunSucceededAt { get; init; }

    public string NumberDisplay => $"#{Number}";
    public bool IsTagVisible => TagName is not null;
    public bool IsAddTagButtonVisible => TagName is null;
    public double CardOpacity => IsClosed ? 0.5 : 1.0;
    public string CloseReopenGlyph => IsClosed ? "\uE72C" : "\uE73E";
    public string CloseReopenTooltip => IsClosed ? "Reopen card" : "Close card";

    public bool IsDoneLane => LaneName == SystemLaneNames.Done;
    public double CardTitleFontSize => IsDoneLane ? 12.0 : 14.0;

    public string LaneDotColour => LaneName switch
    {
        SystemLaneNames.Done => "#5da75d",
        SystemLaneNames.Doing => "#c4904a",
        SystemLaneNames.ToDo => "#5a8ab8",
        _ => "#66667a"
    };

    public bool IsAutoRunFailedIndicatorVisible =>
        CardAutoRunState.For(LastAutoRunFailedAt, LastAutoRunSucceededAt).FailedIndicatorVisible;

    public string AutoRunFailedTooltip =>
        CardAutoRunState.For(LastAutoRunFailedAt, LastAutoRunSucceededAt).FailedTooltip;

    public bool IsAutoRunSucceededIndicatorVisible =>
        CardAutoRunState.For(LastAutoRunFailedAt, LastAutoRunSucceededAt).SucceededIndicatorVisible;

    public string AutoRunSucceededTooltip =>
        CardAutoRunState.For(LastAutoRunFailedAt, LastAutoRunSucceededAt).SucceededTooltip;

    public bool IsSkillsButtonVisible { get; init; }
    public bool IsInProgress { get; init; }

    public bool MatchesSearch(string searchText) =>
        CardSearch.Matches(Title, TagName, Number, Description, searchText);
}

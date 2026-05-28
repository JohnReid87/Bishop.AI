using Bishop.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels;

public sealed partial class CardViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LaneName { get; init; } = string.Empty;
    public string? TagName { get; init; }
    public string? TagColour { get; init; }
    public bool IsClosed { get; init; }
    public int? GitHubIssueNumber { get; init; }
    public Guid? BatchId { get; init; }
    public string? BatchName { get; init; }
    public DateTimeOffset? BatchCreatedAt { get; init; }

    [ObservableProperty]
    private bool _isSelected;
    public DateTimeOffset? GitHubPushedAt { get; init; }
    public DateTimeOffset? LastAutoRunFailedAt { get; init; }

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

    public bool IsAutoRunFailedIndicatorVisible => LastAutoRunFailedAt.HasValue;
    public string AutoRunFailedTooltip => LastAutoRunFailedAt.HasValue ? $"Auto-run failed at {LastAutoRunFailedAt.Value:yyyy-MM-dd HH:mm}" : string.Empty;

    public bool IsSkillsButtonVisible { get; init; }
    public bool IsInProgress { get; init; }

    public bool MatchesSearch(string searchText)
    {
        var query = searchText.StartsWith('#') ? searchText[1..] : searchText;
        return Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (TagName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
               Number.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

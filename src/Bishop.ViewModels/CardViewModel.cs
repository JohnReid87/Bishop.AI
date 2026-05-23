using Bishop.Core;

namespace Bishop.ViewModels;

public sealed class CardViewModel
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
    public DateTimeOffset? GitHubPushedAt { get; init; }

    public string NumberDisplay => $"#{Number}";
    public bool IsTagVisible => TagName is not null;
    public bool IsAddTagButtonVisible => TagName is null;
    public double CardOpacity => IsClosed ? 0.5 : 1.0;
    public string CloseReopenGlyph => IsClosed ? "" : "";
    public string CloseReopenTooltip => IsClosed ? "Reopen card" : "Close card";

    public bool IsDoneLane => LaneName == SystemLaneNames.Done;
    public double CardTitleFontSize => IsDoneLane ? 12.0 : 14.0;

    // Set by WorkspaceDetailPage before cards are rendered so the one-time x:Bind reads the correct value.
    public static bool IsCardSkillsButtonVisible { get; set; }
    public bool IsSkillsButtonVisible => IsCardSkillsButtonVisible;

    public bool MatchesSearch(string searchText) =>
        Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        (TagName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
}

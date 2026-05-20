using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed class CardViewModel
{
    public Guid Id { get; init; }
    public Guid LaneId { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LaneName { get; init; } = string.Empty;
    public IReadOnlyList<CardTagViewModel> Tags { get; init; } = [];
    public string? FirstTagName { get; init; }
    public string? FirstTagColour { get; init; }
    public bool IsClosed { get; init; }
    public int? GitHubIssueNumber { get; init; }
    public DateTimeOffset? GitHubPushedAt { get; init; }

    public string NumberDisplay => $"#{Number}";
    public Visibility TagChipVisibility => FirstTagName is not null ? Visibility.Visible : Visibility.Collapsed;
    public double CardOpacity => IsClosed ? 0.5 : 1.0;
    public string CloseReopenGlyph => IsClosed ? "" : "";
    public string CloseReopenTooltip => IsClosed ? "Reopen card" : "Close card";

    public bool IsDoneLane => LaneName == "Done";
    public Thickness CardHeaderPadding => IsDoneLane ? new Thickness(10, 2, 4, 2) : new Thickness(10, 4, 4, 4);
    public Thickness CardTitlePadding => IsDoneLane ? new Thickness(10, 3, 10, 4) : new Thickness(10, 6, 10, 8);
    public double CardTitleFontSize => IsDoneLane ? 12.0 : 14.0;

    // Set by WorkspaceDetailPage before cards are rendered so the one-time x:Bind reads the correct value.
    public static Visibility CardSkillsButtonVisibility { get; set; } = Visibility.Collapsed;
    public Visibility SkillsButtonVisibility => CardSkillsButtonVisibility;
}

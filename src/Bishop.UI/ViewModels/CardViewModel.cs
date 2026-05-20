using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed partial class CardViewModel : ObservableObject
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

    // Set by WorkspaceDetailPage before cards are rendered so the one-time x:Bind reads the correct value.
    public static Visibility CardSkillsButtonVisibility { get; set; } = Visibility.Collapsed;
    public Visibility SkillsButtonVisibility => CardSkillsButtonVisibility;

    public bool IsDoneLane => LaneName == "Done";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyVisibility))]
    [NotifyPropertyChangedFor(nameof(HeaderBorderThickness))]
    public partial bool IsExpanded { get; set; }

    public Visibility BodyVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public Thickness HeaderBorderThickness => IsExpanded ? new Thickness(0, 0, 0, 1) : new Thickness(0);

    public void ToggleExpand() => IsExpanded = !IsExpanded;
}

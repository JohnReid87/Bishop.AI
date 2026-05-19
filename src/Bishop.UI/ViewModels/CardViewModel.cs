using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed class CardViewModel
{
    public Guid Id { get; init; }
    public Guid LaneId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? FirstTagName { get; init; }
    public string? FirstTagColour { get; init; }

    public string ShortId => Id.ToString("N")[..2];
    public Visibility TagChipVisibility => FirstTagName is not null ? Visibility.Visible : Visibility.Collapsed;

    // Set by WorkspaceDetailPage before cards are rendered so the one-time x:Bind reads the correct value.
    public static Visibility CardSkillsButtonVisibility { get; set; } = Visibility.Collapsed;
    public Visibility SkillsButtonVisibility => CardSkillsButtonVisibility;
}

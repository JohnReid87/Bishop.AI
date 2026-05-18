using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed class CardViewModel
{
    public Guid Id { get; init; }
    public Guid LaneId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];

    // Set by WorkspaceDetailPage before cards are rendered so the one-time x:Bind reads the correct value.
    public static Visibility CardSkillsButtonVisibility { get; set; } = Visibility.Collapsed;
    public Visibility SkillsButtonVisibility => CardSkillsButtonVisibility;
}

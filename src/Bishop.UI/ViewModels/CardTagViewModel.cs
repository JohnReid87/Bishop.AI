using Bishop.Core;

namespace Bishop.UI.ViewModels;

public sealed class CardTagViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Colour { get; init; } = BrandTagPalette.DefaultColour;
}

namespace Bishop.Core;

public static class BrandTagPalette
{
    public const string DefaultColour = "#888888";

    public static IReadOnlyDictionary<string, string> DefaultColours { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TagNames.Arch] = "#6b8caf",
            [TagNames.Bug] = "#c97a8a",
            [TagNames.Chore] = "#a89878",
            [TagNames.Docs] = "#5fa89c",
            [TagNames.Feature] = "#7fa87a",
            [TagNames.Spike] = "#9a7ab8",
            [TagNames.Test] = "#c4a85f",
        };
}

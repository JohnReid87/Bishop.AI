namespace Bishop.Core;

public static class BrandTagPalette
{
    public const string DefaultColour = "#888888";

    public static IReadOnlyDictionary<string, string> DefaultColours { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TagNames.Arch] = "#6b8caf",
            [TagNames.Bug] = "#c97a8a",
            [TagNames.Chore] = "#9aa86a",
            [TagNames.Docs] = "#5fa89c",
            [TagNames.Feature] = "#7fa87a",
            [TagNames.Security] = "#7878bc",
            [TagNames.Spike] = "#9a7ab8",
            [TagNames.Test] = "#c4a85f",
        };
}

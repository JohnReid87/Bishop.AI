namespace Bishop.Core;

public static class BrandTagPalette
{
    public static IReadOnlyDictionary<string, string> DefaultColours { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["arch"] = "#6b8caf",
            ["bug"] = "#c97a8a",
            ["chore"] = "#a89878",
            ["docs"] = "#5fa89c",
            ["feature"] = "#7fa87a",
            ["spike"] = "#9a7ab8",
            ["test"] = "#c4a85f",
        };

    public static IReadOnlyList<string> DefaultNames { get; } =
        ["feature", "bug", "chore", "docs", "arch", "test", "spike"];
}

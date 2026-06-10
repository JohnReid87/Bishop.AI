using Bishop.App.Services.Terminal;

namespace Bishop.App.Context.ContextPack;

internal static class StaticContextSections
{
    private static readonly Lazy<ParsedSections> _parsed = new(LoadAndParse);

    internal static int ParseInvocationCount;

    public static IReadOnlyDictionary<string, string> Slice(IReadOnlyList<string> requiredSections)
    {
        var parsed = _parsed.Value;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in requiredSections)
        {
            if (!parsed.ByName.TryGetValue(name, out var sectionBody))
                throw new InvalidOperationException(
                    $"Unknown context section \"{name}\". Valid sections: {parsed.ValidNames}");

            result[name] = sectionBody;
        }

        return result;
    }

    private static ParsedSections LoadAndParse()
    {
        Interlocked.Increment(ref ParseInvocationCount);

        var body = WorkspaceContextSeeder.LoadStaticBody();
        var sections = PrintContextQueryHandler.ParseH2Sections(body);

        var byName = sections.ToDictionary(
            s => s.Name,
            s => s.Body.TrimEnd(),
            StringComparer.OrdinalIgnoreCase);
        var validNames = string.Join(", ", sections.Select(s => $"\"{s.Name}\""));

        return new ParsedSections(byName, validNames);
    }

    private sealed record ParsedSections(IReadOnlyDictionary<string, string> ByName, string ValidNames);
}

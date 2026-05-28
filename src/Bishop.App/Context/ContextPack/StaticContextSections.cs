using Bishop.App.Services.Terminal;

namespace Bishop.App.Context.ContextPack;

internal static class StaticContextSections
{
    public static IReadOnlyDictionary<string, string> Slice(IReadOnlyList<string> requiredSections)
    {
        var body = WorkspaceContextSeeder.LoadStaticBody();
        var parsed = PrintContextQueryHandler.ParseH2Sections(body);

        var byName = parsed.ToDictionary(s => s.Name, s => s.Body, StringComparer.OrdinalIgnoreCase);
        var validNames = string.Join(", ", parsed.Select(s => $"\"{s.Name}\""));

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in requiredSections)
        {
            if (!byName.TryGetValue(name, out var sectionBody))
                throw new InvalidOperationException(
                    $"Unknown context section \"{name}\". Valid sections: {validNames}");

            result[name] = sectionBody.TrimEnd();
        }

        return result;
    }
}

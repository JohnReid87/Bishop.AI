using MediatR;

namespace Bishop.App.Context;

public sealed class PrintContextQueryHandler : IRequestHandler<PrintContextQuery, string>
{
    private const string BishopContextPath = ".bishop/BISHOP_CONTEXT.md";

    public Task<string> Handle(PrintContextQuery request, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(request.WorkspacePath, ".bishop", "BISHOP_CONTEXT.md");
        if (!File.Exists(filePath))
            throw new InvalidOperationException(
                $"Context file not found at '{BishopContextPath}'. Has the workspace been launched?");

        var content = File.ReadAllText(filePath);
        var sections = ParseH2Sections(content);

        if (request.SectionName is null)
        {
            var names = string.Join(", ", sections.Select(s => s.Name));
            var hint = $"# Sections: {names} — use 'bishop context print --section \"NAME\"' to scope";
            return Task.FromResult(hint + Environment.NewLine + content);
        }

        var match = sections.FirstOrDefault(s =>
            string.Equals(s.Name, request.SectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var validNames = string.Join(", ", sections.Select(s => $"\"{s.Name}\""));
            throw new InvalidOperationException(
                $"Unknown section \"{request.SectionName}\". Valid sections: {validNames}");
        }

        return Task.FromResult(match.Body.TrimEnd());
    }

    internal static List<Section> ParseH2Sections(string content)
    {
        var lines = content.Split('\n');
        var sections = new List<Section>();
        int? sectionStart = null;
        string? currentName = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (sectionStart.HasValue && currentName is not null)
                {
                    var bodyLines = lines[sectionStart.Value..i].Select(l => l.TrimEnd('\r'));
                    sections.Add(new Section(currentName, string.Join(Environment.NewLine, bodyLines)));
                }

                currentName = StripLabel(line[3..].Trim());
                sectionStart = i;
            }
        }

        if (sectionStart.HasValue && currentName is not null)
        {
            var bodyLines = lines[sectionStart.Value..].Select(l => l.TrimEnd('\r'));
            sections.Add(new Section(currentName, string.Join(Environment.NewLine, bodyLines)));
        }

        return sections;
    }

    internal static string StripLabel(string headerText)
    {
        var lastParen = headerText.LastIndexOf('(');
        if (lastParen > 0 && headerText[^1] == ')')
            return headerText[..lastParen].Trim();
        return headerText;
    }

    internal sealed record Section(string Name, string Body);
}

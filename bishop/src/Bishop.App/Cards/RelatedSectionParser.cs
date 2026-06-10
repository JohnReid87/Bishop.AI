using System.Text.RegularExpressions;

namespace Bishop.App.Cards;

public static partial class RelatedSectionParser
{
    [GeneratedRegex(@"(?<![:/\w])#?(\d+)\b")]
    private static partial Regex CardRefRegex();

    public static IReadOnlyList<int> ParseCardNumbers(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return [];

        var numbers = new List<int>();
        var inRelated = false;

        foreach (var rawLine in description.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line == "### Related")
            {
                inRelated = true;
                continue;
            }

            if (inRelated && line.StartsWith("### "))
                break;

            if (!inRelated)
                continue;

            foreach (Match m in CardRefRegex().Matches(line))
                numbers.Add(int.Parse(m.Groups[1].Value));
        }

        return numbers;
    }
}

using System.Text;

namespace Bishop.App.Services.Claude;

internal static class StreamTextFormatting
{
    public const int MaxSummaryLength = 120;

    public static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && !lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        if (lastWasSpace) sb.Length--;
        return sb.ToString();
    }

    public static string Truncate(string s)
        => s.Length > MaxSummaryLength ? s[..MaxSummaryLength] + "…" : s;
}

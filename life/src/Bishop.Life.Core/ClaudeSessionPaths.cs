namespace Bishop.Life.Core;

/// <summary>
/// Resolves the on-disk Claude Code session JSONL path for a given working
/// directory + session id. Claude Code writes one JSONL file per session under
/// <c>%USERPROFILE%\.claude\projects\&lt;encoded-cwd&gt;\&lt;session-id&gt;.jsonl</c>,
/// where the encoded cwd replaces the path-separator family (<c>\</c>, <c>/</c>,
/// <c>:</c>, <c>.</c>) with <c>-</c>.
/// </summary>
public static class ClaudeSessionPaths
{
    /// <summary>
    /// Encodes <paramref name="cwd"/> the way Claude Code does when naming its
    /// per-project session directory. See class doc for the substitution set.
    /// </summary>
    public static string EncodeCwd(string cwd)
    {
        if (string.IsNullOrEmpty(cwd)) return string.Empty;
        var chars = cwd.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c == '\\' || c == '/' || c == ':' || c == '.') chars[i] = '-';
        }
        return new string(chars);
    }

    public static string ResolveSessionFilePath(string cwd, string sessionId)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", "projects", EncodeCwd(cwd), sessionId + ".jsonl");
    }
}

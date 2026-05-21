namespace Bishop.App.Claude;

public sealed class ClaudeExecutableResolver : IClaudeExecutableResolver
{
    private static readonly string[] DefaultPathExt = [".COM", ".EXE", ".BAT", ".CMD"];

    private readonly Func<string, string?> _getEnv;
    private readonly Func<string, bool> _fileExists;
    private readonly bool _isWindows;
    private readonly object _gate = new();

    private string? _cached;

    public ClaudeExecutableResolver()
        : this(Environment.GetEnvironmentVariable, File.Exists, OperatingSystem.IsWindows())
    {
    }

    public ClaudeExecutableResolver(
        Func<string, string?> getEnv,
        Func<string, bool> fileExists,
        bool isWindows)
    {
        _getEnv = getEnv;
        _fileExists = fileExists;
        _isWindows = isWindows;
    }

    public string Resolve()
    {
        if (_cached is not null) return _cached;

        lock (_gate)
        {
            if (_cached is not null) return _cached;

            var directories = ReadDirectories();
            var candidates = BuildCandidateNames();

            foreach (var dir in directories)
            {
                foreach (var name in candidates)
                {
                    var full = Path.Combine(dir, name);
                    if (_fileExists(full))
                    {
                        _cached = full;
                        return full;
                    }
                }
            }

            throw new ClaudeNotFoundException(candidates, directories);
        }
    }

    private IReadOnlyList<string> ReadDirectories()
    {
        var pathEnv = _getEnv("PATH") ?? string.Empty;
        return pathEnv
            .Split(Path.PathSeparator)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private IReadOnlyList<string> BuildCandidateNames()
    {
        if (!_isWindows) return ["claude"];

        var pathExt = _getEnv("PATHEXT");
        var extensions = string.IsNullOrWhiteSpace(pathExt)
            ? DefaultPathExt
            : pathExt
                .Split(';')
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToArray();

        return extensions.Select(e => "claude" + e).ToArray();
    }
}

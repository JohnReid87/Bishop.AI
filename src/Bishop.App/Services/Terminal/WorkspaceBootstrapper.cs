using System.Diagnostics;

namespace Bishop.App.Services.Terminal;

internal sealed class WorkspaceBootstrapper : IWorkspaceBootstrapper
{
    internal const string GitIgnoreFileName = ".gitignore";
    internal const string BishopIgnoreEntry = ".bishop/";
    internal const string SlopwatchIgnoreEntry = ".slopwatch/";

    internal static readonly string[] LegacyGranularEntries =
    {
        ".bishop/runs/",
        ".bishop/denials.jsonl",
    };

    public async Task EnsureBootstrappedAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        Directory.CreateDirectory(workspacePath);

        await EnsureGitInitialisedAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await EnsureGitIgnoreAsync(workspacePath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureGitInitialisedAsync(string workspacePath, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(workspacePath, ".git")))
            return;

        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspacePath,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("init");
        using var proc = Process.Start(psi);
        if (proc is not null)
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureGitIgnoreAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(workspacePath, GitIgnoreFileName);
        var existing = File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
        var merged = EnsureGitIgnoreEntries(existing);
        if (!string.Equals(existing, merged, StringComparison.Ordinal))
            await File.WriteAllTextAsync(path, merged, cancellationToken).ConfigureAwait(false);
    }

    internal static string EnsureGitIgnoreEntries(string? existing)
    {
        var required = new[] { BishopIgnoreEntry, SlopwatchIgnoreEntry };

        if (existing is null)
            return string.Join(Environment.NewLine, required) + Environment.NewLine;

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var filtered = lines
            .Where(l => !LegacyGranularEntries.Any(e => string.Equals(l, e, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var result = filtered.Count == lines.Count
            ? existing
            : string.Join(newline, filtered);

        foreach (var entry in required)
        {
            var current = result.Split('\n').Select(l => l.TrimEnd('\r'));
            if (current.Any(l => string.Equals(l, entry, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (result.Length > 0 && !result.EndsWith("\n", StringComparison.Ordinal))
                result += newline;
            result += entry + newline;
        }

        return result;
    }
}

using System.Diagnostics;
using System.Text.Json;

namespace Bishop.App.Services.Terminal;

internal sealed class WorkspaceBootstrapper : IWorkspaceBootstrapper
{
    internal const string GitIgnoreFileName = ".gitignore";
    internal const string BishopIgnoreEntry = ".bishop/";
    internal const string SlopwatchIgnoreEntry = ".slopwatch/";

    internal const string SlopwatchPackageId = "slopwatch.cmd";
    internal const string SlopwatchVersion = "0.4.0";
    internal const string ToolManifestRelativePath = ".config/dotnet-tools.json";

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
        await EnsureSlopwatchInstalledAsync(workspacePath, cancellationToken).ConfigureAwait(false);
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

    private static async Task EnsureSlopwatchInstalledAsync(string workspacePath, CancellationToken cancellationToken)
    {
        if (!IsDotNetWorkspace(workspacePath))
            return;

        var manifestPath = Path.Combine(workspacePath, ToolManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            await RunDotnetAsync(workspacePath, cancellationToken, "new", "tool-manifest").ConfigureAwait(false);
        }

        var manifestJson = File.Exists(manifestPath)
            ? await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false)
            : null;

        if (IsSlopwatchInManifest(manifestJson))
            return;

        await RunDotnetAsync(
            workspacePath,
            cancellationToken,
            "tool", "install", SlopwatchPackageId, "--local", "--version", SlopwatchVersion).ConfigureAwait(false);
    }

    internal static bool IsDotNetWorkspace(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
            return false;

        return Directory.EnumerateFiles(workspacePath, "*.sln", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(workspacePath, "*.csproj", SearchOption.TopDirectoryOnly).Any();
    }

    internal static bool IsSlopwatchInManifest(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (!doc.RootElement.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var prop in tools.EnumerateObject())
            {
                if (string.Equals(prop.Name, SlopwatchPackageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static async Task RunDotnetAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi);
        if (proc is not null)
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
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

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bishop.App.Services.Terminal;

internal sealed class WorkspaceBootstrapper : IWorkspaceBootstrapper
{
    internal const string GitIgnoreFileName = ".gitignore";
    internal const string BishopIgnoreEntry = ".bishop/";
    internal const string SlopwatchIgnoreEntry = ".slopwatch/";

    internal const string SlopwatchPackageId = "slopwatch.cmd";
    internal const string SlopwatchVersion = "0.4.0";
    internal const string ToolManifestRelativePath = ".config/dotnet-tools.json";

    internal const string ClaudeSettingsRelativePath = ".claude/settings.json";

    internal static readonly string[] LegacyGranularEntries =
    {
        ".bishop/runs/",
        ".bishop/denials.jsonl",
    };

    internal static readonly string[] ClaudeAllowList =
    {
        "Bash(bishop:*)",
        "PowerShell(bishop:*)",
        "Write(.bishop/**)",
        "Read(.bishop/**)",
        "Bash(dotnet build:*)",
        "Bash(dotnet test:*)",
        "Bash(dotnet tool run slopwatch:*)",
        "Bash(git add:*)",
        "Bash(git commit:*)",
        "Bash(git status:*)",
        "Bash(git diff:*)",
        "Bash(git log:*)",
        "Bash(git rev-parse:*)",
    };

    internal static readonly string[] LegacyClaudeAllowEntries =
    {
        "Write(./.bishop/**)",
        "Read(./.bishop/**)",
    };

    public async Task EnsureBootstrappedAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        Directory.CreateDirectory(workspacePath);

        await EnsureGitInitialisedAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await EnsureGitIgnoreAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await EnsureClaudeSettingsAsync(workspacePath, cancellationToken).ConfigureAwait(false);
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

    private static async Task EnsureClaudeSettingsAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(workspacePath, ClaudeSettingsRelativePath);
        var existing = File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
        var merged = MergeClaudeSettings(existing, ClaudeAllowList);
        if (string.Equals(existing, merged, StringComparison.Ordinal))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, merged, cancellationToken).ConfigureAwait(false);
    }

    internal static string MergeClaudeSettings(string? existingJson, IEnumerable<string> required)
    {
        var requiredList = required.ToList();

        if (string.IsNullOrWhiteSpace(existingJson))
            return SerializeClaudeSettings(BuildFreshClaudeSettings(requiredList));

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(existingJson);
        }
        catch (JsonException)
        {
            return existingJson;
        }

        if (root is not JsonObject rootObj)
            return existingJson;

        if (rootObj["permissions"] is not JsonObject perms)
        {
            perms = new JsonObject();
            rootObj["permissions"] = perms;
        }

        if (perms["allow"] is not JsonArray allow)
        {
            allow = new JsonArray();
            perms["allow"] = allow;
        }

        var legacy = new HashSet<string>(LegacyClaudeAllowEntries, StringComparer.Ordinal);
        var changed = false;
        for (var i = allow.Count - 1; i >= 0; i--)
        {
            if (allow[i] is JsonValue val && val.TryGetValue<string>(out var s) && legacy.Contains(s))
            {
                allow.RemoveAt(i);
                changed = true;
            }
        }

        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in allow)
        {
            if (node is JsonValue val && val.TryGetValue<string>(out var s))
                present.Add(s);
        }

        foreach (var entry in requiredList)
        {
            if (present.Add(entry))
            {
                allow.Add(JsonValue.Create(entry));
                changed = true;
            }
        }

        return changed ? SerializeClaudeSettings(rootObj) : existingJson;
    }

    private static JsonObject BuildFreshClaudeSettings(IEnumerable<string> required)
    {
        var allow = new JsonArray();
        foreach (var entry in required)
            allow.Add(JsonValue.Create(entry));

        return new JsonObject
        {
            ["permissions"] = new JsonObject { ["allow"] = allow },
        };
    }

    private static string SerializeClaudeSettings(JsonNode node)
        => node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

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

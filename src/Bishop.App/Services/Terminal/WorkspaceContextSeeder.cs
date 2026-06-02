using System.Reflection;
using System.Text;
using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Services.Terminal;

internal sealed class WorkspaceContextSeeder : IWorkspaceContextSeeder
{
    internal const string BishopFolder = ".bishop";
    internal const string BishopContextFileName = "BISHOP_CONTEXT.md";
    internal const string ContextFileName = "CONTEXT.md";
    internal const string ClaudeMdFileName = "CLAUDE.md";
    internal const string PointerLine = "> See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) — Bishop CLI reference and live workspace state for LLM agents.";
    internal const string PointerMarker = ".bishop/BISHOP_CONTEXT.md";
    internal const string LegacyPointerLine = "> See [BISHOP_CONTEXT.md](./BISHOP_CONTEXT.md) — Bishop CLI reference and live workspace state for LLM agents.";

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public WorkspaceContextSeeder(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task SeedAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workspacePath))
            return;

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
        if (IsShallowOrSensitivePath(fullPath))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var workspace = await ResolveWorkspaceAsync(db, fullPath, cancellationToken).ConfigureAwait(false);
        if (workspace is null) return;

        var bishopDir = Path.Combine(fullPath, BishopFolder);
        MigrateLegacyFiles(fullPath, bishopDir);
        Directory.CreateDirectory(bishopDir);

        var bishopFile = Path.Combine(bishopDir, BishopContextFileName);
        await File.WriteAllTextAsync(bishopFile, BuildBishopContext(workspace), cancellationToken).ConfigureAwait(false);

        var contextFile = Path.Combine(fullPath, ContextFileName);
        var existing = File.Exists(contextFile)
            ? await File.ReadAllTextAsync(contextFile, cancellationToken).ConfigureAwait(false)
            : null;
        var merged = EnsureContextMd(existing, workspace);
        if (!string.Equals(existing, merged, StringComparison.Ordinal))
            await File.WriteAllTextAsync(contextFile, merged, cancellationToken).ConfigureAwait(false);

        var claudeFile = Path.Combine(fullPath, ClaudeMdFileName);
        var existingClaude = File.Exists(claudeFile)
            ? await File.ReadAllTextAsync(claudeFile, cancellationToken).ConfigureAwait(false)
            : null;
        var mergedClaude = EnsureClaudeMd(existingClaude, workspace);
        if (!string.Equals(existingClaude, mergedClaude, StringComparison.Ordinal))
            await File.WriteAllTextAsync(claudeFile, mergedClaude, cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsShallowOrSensitivePath(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (root == null) return false;

        var relative = fullPath[root.Length..];
        var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return true;

        var sensitiveDirectories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        return sensitiveDirectories
            .Where(d => !string.IsNullOrEmpty(d))
            .Any(d => string.Equals(fullPath, d, StringComparison.OrdinalIgnoreCase));
    }

    internal static void MigrateLegacyFiles(string fullPath, string bishopDir)
    {
        var legacyContext = Path.Combine(fullPath, BishopContextFileName);
        var newContext = Path.Combine(bishopDir, BishopContextFileName);
        if (File.Exists(legacyContext) && !File.Exists(newContext))
        {
            Directory.CreateDirectory(bishopDir);
            File.Move(legacyContext, newContext);
        }

        var legacyNotes = Path.Combine(fullPath, "BISHOP_NOTES.md");
        var newNotes = Path.Combine(bishopDir, "BISHOP_NOTES.md");
        if (File.Exists(legacyNotes) && !File.Exists(newNotes))
        {
            Directory.CreateDirectory(bishopDir);
            File.Move(legacyNotes, newNotes);
        }
    }

    private static async Task<Workspace?> ResolveWorkspaceAsync(BishopDbContext db, string fullPath, CancellationToken cancellationToken)
    {
        var normalizedPathLower = fullPath.ToLowerInvariant();

        return await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Path.ToLower() == normalizedPathLower, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static string BuildBishopContext(Workspace workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# BISHOP_CONTEXT — {workspace.Name}");
        sb.AppendLine();
        sb.AppendLine("> Auto-generated by Bishop on every workspace launch. Do not edit by hand —");
        sb.AppendLine("> changes will be overwritten. Update the workspace itself (via the app or CLI)");
        sb.AppendLine("> and relaunch to refresh.");
        sb.AppendLine();
        sb.AppendLine("This file describes the current Bishop workspace so an LLM agent working in");
        sb.AppendLine("this directory has everything it needs to interact with the board correctly.");
        sb.AppendLine();

        sb.AppendLine("## This workspace");
        sb.AppendLine();
        sb.AppendLine($"- **Name:** {workspace.Name}");
        sb.AppendLine($"- **Path:** `{workspace.Path}`");
        if (!string.IsNullOrWhiteSpace(workspace.GitHubRepo))
            sb.AppendLine($"- **GitHub:** `{workspace.GitHubRepo}`");
        sb.AppendLine();

        sb.AppendLine("### Lanes");
        sb.AppendLine();
        for (var i = 0; i < SystemLaneNames.All.Count; i++)
            sb.AppendLine($"{i + 1}. {SystemLaneNames.All[i]}");
        sb.AppendLine();

        sb.AppendLine("### Tags");
        sb.AppendLine();
        foreach (var name in BrandTagPalette.DefaultColours.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"- `{name}`");
        sb.AppendLine();

        sb.Append(LoadStaticBody());

        return sb.ToString();
    }

    internal static string LoadStaticBody()
    {
        var assembly = typeof(WorkspaceContextSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "Bishop.App.Services.Terminal.BishopContext.static.md")!;
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        return raw.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }

    internal static string EnsureContextMd(string? existing, Workspace workspace) =>
        EnsurePointerInFile(existing, () => BuildContextMdStub(workspace));

    internal static string EnsureClaudeMd(string? existing, Workspace workspace) =>
        EnsurePointerInFile(existing, () => BuildClaudeMdStub(workspace));

    private static string EnsurePointerInFile(string? existing, Func<string> buildStub)
    {
        if (existing is null)
            return buildStub();

        var rewritten = existing.Replace(LegacyPointerLine, PointerLine, StringComparison.Ordinal);
        if (rewritten.Contains(PointerMarker, StringComparison.Ordinal))
            return rewritten;

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split('\n').ToList();
        var h1Index = lines.FindIndex(l => l.TrimEnd('\r').StartsWith("# ", StringComparison.Ordinal));

        var pointerBlock = new[] { "", PointerLine, "" };
        if (h1Index >= 0)
        {
            lines.InsertRange(h1Index + 1, pointerBlock);
        }
        else
        {
            lines.InsertRange(0, new[] { PointerLine, "" });
        }

        return string.Join(newline, lines.Select(l => l.TrimEnd('\r')));
    }

    private static string BuildContextMdStub(Workspace workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {workspace.Name}");
        sb.AppendLine();
        sb.AppendLine(PointerLine);
        sb.AppendLine();
        sb.AppendLine("<!-- Add a description of this workspace here: what it is, who uses it, and");
        sb.AppendLine("     the conventions a contributor (human or LLM) needs to know. -->");
        return sb.ToString();
    }

    private static string BuildClaudeMdStub(Workspace workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {workspace.Name}");
        sb.AppendLine();
        sb.AppendLine(PointerLine);
        return sb.ToString();
    }
}

using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Core;
using MediatR;

namespace Bishop.Cli;

internal sealed class WorkspaceResolver(IMediator mediator)
{
    public async Task<Workspace> ResolveAsync(string? workspaceOption, CancellationToken cancellationToken = default)
    {
        var workspaces = await mediator.Send(new ListWorkspacesQuery(), cancellationToken);

        if (!string.IsNullOrEmpty(workspaceOption))
        {
            var byName = workspaces.FirstOrDefault(w =>
                string.Equals(w.Name, workspaceOption, StringComparison.OrdinalIgnoreCase));
            if (byName is not null) return byName;

            var normalizedOption = Path.GetFullPath(workspaceOption);
            var byPath = workspaces.FirstOrDefault(w =>
                !string.IsNullOrEmpty(w.Path) &&
                string.Equals(Path.GetFullPath(w.Path), normalizedOption, StringComparison.OrdinalIgnoreCase));
            if (byPath is not null) return byPath;

            throw new InvalidOperationException($"No workspace found matching '{workspaceOption}'.");
        }

        var cwd = Directory.GetCurrentDirectory();
        var candidate = workspaces
            .Where(w => !string.IsNullOrEmpty(w.Path))
            .Where(w => IsAncestorOrEqual(w.Path, cwd))
            .OrderByDescending(w => w.Path.Length)
            .FirstOrDefault();

        if (candidate is null)
            throw new InvalidOperationException(
                "Not inside a known workspace directory. Use --workspace to specify one.");

        return candidate;
    }

    private static bool IsAncestorOrEqual(string workspacePath, string cwd)
    {
        var normalized = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCwd = Path.GetFullPath(cwd)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedCwd.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
            && (normalizedCwd.Length == normalized.Length
                || normalizedCwd[normalized.Length] == Path.DirectorySeparatorChar);
    }
}

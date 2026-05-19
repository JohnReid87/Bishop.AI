using System.ComponentModel;
using System.Diagnostics;

namespace Bishop.App.Git;

public sealed class GitCli : IGitCli
{
    public async Task<GetRecentCommitsResult> GetRecentCommitsAsync(
        string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspacePath,
        };
        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add("--max-count=5");
        psi.ArgumentList.Add("--format=%h\x1f%H\x1f%s\x1f%aI");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new GetRecentCommitsResult.GitNotFound();
        }

        if (proc is null)
            return new GetRecentCommitsResult.GitNotFound();

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 128)
            {
                if (stderr.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
                    return new GetRecentCommitsResult.NotAGitRepo();
                if (stderr.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase))
                    return new GetRecentCommitsResult.NoCommits();
                throw new InvalidOperationException($"git exited {proc.ExitCode}: {stderr.Trim()}");
            }

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git exited {proc.ExitCode}: {stderr.Trim()}");

            var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return new GetRecentCommitsResult.NoCommits();

            var commits = new List<CommitInfo>(lines.Length);
            foreach (var line in lines)
            {
                var parts = line.Split('\x1f');
                if (parts.Length != 4) continue;
                if (!DateTimeOffset.TryParse(parts[3].Trim(), out var ts)) continue;
                commits.Add(new CommitInfo(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), ts));
            }

            return commits.Count == 0
                ? new GetRecentCommitsResult.NoCommits()
                : new GetRecentCommitsResult.Success(commits);
        }
    }
}

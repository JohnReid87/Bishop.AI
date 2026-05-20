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
        psi.ArgumentList.Add("--format=%h\x1f%H\x1f%B\x1f%aI\x1e");

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

            var records = stdout.Split('\x1e', StringSplitOptions.RemoveEmptyEntries);
            if (records.Length == 0)
                return new GetRecentCommitsResult.NoCommits();

            var commits = new List<CommitInfo>(records.Length);
            foreach (var record in records)
            {
                var parts = record.Split('\x1f');
                if (parts.Length != 4) continue;
                if (!DateTimeOffset.TryParse(parts[3].Trim(), out var ts)) continue;

                var fullMessage = parts[2];
                var newlineIdx = fullMessage.IndexOf('\n');
                string subject, body;
                if (newlineIdx < 0)
                {
                    subject = fullMessage.Trim();
                    body = "";
                }
                else
                {
                    subject = fullMessage[..newlineIdx].Trim();
                    var remainder = fullMessage[(newlineIdx + 1)..];
                    if (remainder.StartsWith('\n'))
                        remainder = remainder[1..];
                    body = remainder.TrimEnd();
                }

                commits.Add(new CommitInfo(parts[0].Trim(), parts[1].Trim(), subject, body, ts));
            }

            return commits.Count == 0
                ? new GetRecentCommitsResult.NoCommits()
                : new GetRecentCommitsResult.Success(commits);
        }
    }
}

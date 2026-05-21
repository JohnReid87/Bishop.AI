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

                commits.Add(new CommitInfo(parts[0].Trim(), parts[1].Trim(), subject, body, ts, IsPushed: false));
            }

            if (commits.Count == 0)
                return new GetRecentCommitsResult.NoCommits();

            string? upstreamRef = null;
            HashSet<string> unpushedShas = [];

            var upPsi = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workspacePath,
            };
            upPsi.ArgumentList.Add("rev-parse");
            upPsi.ArgumentList.Add("--abbrev-ref");
            upPsi.ArgumentList.Add("--symbolic-full-name");
            upPsi.ArgumentList.Add("@{u}");

            try
            {
                var upProc = Process.Start(upPsi);
                if (upProc is not null)
                {
                    using (upProc)
                    {
                        var upOut = await upProc.StandardOutput.ReadToEndAsync(cancellationToken);
                        await upProc.WaitForExitAsync(cancellationToken);
                        if (upProc.ExitCode == 0)
                        {
                            upstreamRef = upOut.Trim();

                            var revPsi = new ProcessStartInfo("git")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                WorkingDirectory = workspacePath,
                            };
                            revPsi.ArgumentList.Add("rev-list");
                            revPsi.ArgumentList.Add("@{u}..HEAD");

                            var revProc = Process.Start(revPsi);
                            if (revProc is not null)
                            {
                                using (revProc)
                                {
                                    var revOut = await revProc.StandardOutput.ReadToEndAsync(cancellationToken);
                                    await revProc.WaitForExitAsync(cancellationToken);
                                    if (revProc.ExitCode == 0)
                                    {
                                        unpushedShas = revOut
                                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            {
                // git unavailable for upstream check — no upstream
            }

            var finalCommits = upstreamRef is not null
                ? commits.Select(c => c with { IsPushed = !unpushedShas.Contains(c.FullHash) }).ToList()
                : commits;

            return new GetRecentCommitsResult.Success(finalCommits, upstreamRef);
        }
    }

    public async Task<GetCardCommitResult> GetCardCommitAsync(
        int cardNumber, string workspacePath, CancellationToken cancellationToken = default)
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
        psi.ArgumentList.Add("--perl-regexp");
        psi.ArgumentList.Add($"--grep=\\(card #?{cardNumber}\\)");
        psi.ArgumentList.Add("-1");
        psi.ArgumentList.Add("--format=%h\x1f%H\x1f%aI");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new GetCardCommitResult.GitNotFound();
        }

        if (proc is null)
            return new GetCardCommitResult.GitNotFound();

        string shortHash, fullHash;
        DateTimeOffset timestamp;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 128)
            {
                if (stderr.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
                    return new GetCardCommitResult.NotAGitRepo();
                if (stderr.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase))
                    return new GetCardCommitResult.NotFound();
                throw new InvalidOperationException($"git exited {proc.ExitCode}: {stderr.Trim()}");
            }

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git exited {proc.ExitCode}: {stderr.Trim()}");

            var line = stdout.Trim();
            if (string.IsNullOrEmpty(line))
                return new GetCardCommitResult.NotFound();

            var parts = line.Split('\x1f');
            if (parts.Length != 3 || !DateTimeOffset.TryParse(parts[2].Trim(), out timestamp))
                return new GetCardCommitResult.NotFound();

            shortHash = parts[0].Trim();
            fullHash = parts[1].Trim();
        }

        var isPushed = false;

        try
        {
            var upPsi = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workspacePath,
            };
            upPsi.ArgumentList.Add("rev-parse");
            upPsi.ArgumentList.Add("--abbrev-ref");
            upPsi.ArgumentList.Add("--symbolic-full-name");
            upPsi.ArgumentList.Add("@{u}");

            var upProc = Process.Start(upPsi);
            if (upProc is not null)
            {
                using (upProc)
                {
                    await upProc.StandardOutput.ReadToEndAsync(cancellationToken);
                    await upProc.WaitForExitAsync(cancellationToken);
                    if (upProc.ExitCode == 0)
                    {
                        var revPsi = new ProcessStartInfo("git")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WorkingDirectory = workspacePath,
                        };
                        revPsi.ArgumentList.Add("rev-list");
                        revPsi.ArgumentList.Add("@{u}..HEAD");

                        var revProc = Process.Start(revPsi);
                        if (revProc is not null)
                        {
                            using (revProc)
                            {
                                var revOut = await revProc.StandardOutput.ReadToEndAsync(cancellationToken);
                                await revProc.WaitForExitAsync(cancellationToken);
                                if (revProc.ExitCode == 0)
                                {
                                    var unpushedShas = revOut
                                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                                    isPushed = !unpushedShas.Contains(fullHash);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            // git unavailable for upstream check — IsPushed stays false
        }

        return new GetCardCommitResult.Found(new CommitInfo(shortHash, fullHash, Subject: "", Body: "", timestamp, isPushed));
    }

    public async Task<string?> GetOriginUrlAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspacePath,
        };
        psi.ArgumentList.Add("remote");
        psi.ArgumentList.Add("get-url");
        psi.ArgumentList.Add("origin");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return null;
        }

        if (proc is null)
            return null;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0 ? stdout.Trim() : null;
        }
    }

    public async Task<PushResult> PushAsync(
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
        psi.ArgumentList.Add("push");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new PushResult(Success: false, Message: "git executable not found");
        }

        if (proc is null)
            return new PushResult(Success: false, Message: "git executable not found");

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            var message = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
            return new PushResult(
                Success: proc.ExitCode == 0,
                Message: string.IsNullOrEmpty(message) ? null : message);
        }
    }

    public async Task<GetWorkingTreeStatusResult> GetWorkingTreeStatusAsync(
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
        psi.ArgumentList.Add("status");
        psi.ArgumentList.Add("--porcelain");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new GetWorkingTreeStatusResult.GitNotFound();
        }

        if (proc is null)
            return new GetWorkingTreeStatusResult.GitNotFound();

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 128 &&
                stderr.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
            {
                return new GetWorkingTreeStatusResult.NotAGitRepo();
            }

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git exited {proc.ExitCode}: {stderr.Trim()}");

            var paths = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Length > 0)
                .Select(line => line.Length > 3 ? line[3..] : line)
                .ToList();

            return paths.Count == 0
                ? new GetWorkingTreeStatusResult.Clean()
                : new GetWorkingTreeStatusResult.Dirty(paths);
        }
    }
}

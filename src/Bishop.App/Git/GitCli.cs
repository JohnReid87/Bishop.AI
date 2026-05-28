using Bishop.App.Git.GetCardCommit;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using System.ComponentModel;
using System.Diagnostics;

namespace Bishop.App.Git;

public sealed class GitCli : IGitCli
{
    public async Task<GetRecentCommitsResult> GetRecentCommitsAsync(
        string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
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

            var upstreamRef = await GetUpstreamRefAsync(workspacePath, cancellationToken);
            HashSet<string> unpushedShas = [];

            if (upstreamRef is not null)
            {
                var revPsi = CreateGitProcessStartInfo(workspacePath);
                revPsi.ArgumentList.Add("rev-list");
                revPsi.ArgumentList.Add($"{upstreamRef}..HEAD");

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

            var finalCommits = upstreamRef is not null
                ? commits.Select(c => c with { IsPushed = !unpushedShas.Contains(c.FullHash) }).ToList()
                : commits;

            return new GetRecentCommitsResult.Success(finalCommits, upstreamRef);
        }
    }

    public async Task<GetCardCommitResult> GetCardCommitAsync(
        int cardNumber, string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
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
        var upstreamRef = await GetUpstreamRefAsync(workspacePath, cancellationToken);

        if (upstreamRef is not null)
        {
            var revPsi = CreateGitProcessStartInfo(workspacePath);
            revPsi.ArgumentList.Add("rev-list");
            revPsi.ArgumentList.Add($"{upstreamRef}..HEAD");

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

        return new GetCardCommitResult.Found(new CommitInfo(shortHash, fullHash, Subject: "", Body: "", timestamp, isPushed));
    }

    public async Task<GetCardCommitResult> GetCommitByHashAsync(
        string fullHash, string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add(fullHash);
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

        string shortHash, resolvedFullHash;
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
            resolvedFullHash = parts[1].Trim();
        }

        var isPushed = false;
        var upstreamRef = await GetUpstreamRefAsync(workspacePath, cancellationToken);

        if (upstreamRef is not null)
        {
            var revPsi = CreateGitProcessStartInfo(workspacePath);
            revPsi.ArgumentList.Add("rev-list");
            revPsi.ArgumentList.Add($"{upstreamRef}..HEAD");

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
                        isPushed = !unpushedShas.Contains(resolvedFullHash);
                    }
                }
            }
        }

        return new GetCardCommitResult.Found(new CommitInfo(shortHash, resolvedFullHash, Subject: "", Body: "", timestamp, isPushed));
    }

    public async Task<string?> GetOriginUrlAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
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
        var psi = CreateGitProcessStartInfo(workspacePath);
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

    public async Task<PushResult> PushNewBranchAsync(
        string worktreePath, string branchName, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(worktreePath);
        psi.ArgumentList.Add("push");
        psi.ArgumentList.Add("-u");
        psi.ArgumentList.Add("origin");
        psi.ArgumentList.Add(branchName);

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

    public async Task ResetHardAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("reset");
        psi.ArgumentList.Add("--hard");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git reset --hard exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task CleanWorkingTreeAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("clean");
        psi.ArgumentList.Add("-fd");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git clean -fd exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task<GetWorkingTreeStatusResult> GetWorkingTreeStatusAsync(
        string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
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

    public async Task<int?> GetCommitCountSinceAsync(
        string sha, string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("rev-list");
        psi.ArgumentList.Add("--count");
        psi.ArgumentList.Add($"{sha}..HEAD");

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

            if (proc.ExitCode != 0)
                return null;

            return int.TryParse(stdout.Trim(), out var count) ? count : null;
        }
    }

    public async Task CreateWorktreeAsync(
        string workspacePath, string branchName, string baseBranch, string worktreePath,
        CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("worktree");
        psi.ArgumentList.Add("add");
        psi.ArgumentList.Add("-b");
        psi.ArgumentList.Add(branchName);
        psi.ArgumentList.Add(worktreePath);
        psi.ArgumentList.Add(baseBranch);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git worktree add exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task RemoveWorktreeAsync(
        string workspacePath, string worktreePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("worktree");
        psi.ArgumentList.Add("remove");
        psi.ArgumentList.Add(worktreePath);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git worktree remove exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task<string> GetCurrentBranchAsync(
        string worktreePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(worktreePath);
        psi.ArgumentList.Add("rev-parse");
        psi.ArgumentList.Add("--abbrev-ref");
        psi.ArgumentList.Add("HEAD");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git rev-parse exited {proc.ExitCode}: {stderr.Trim()}");

            return stdout.Trim();
        }
    }

    public async Task<bool> LocalBranchExistsAsync(
        string workspacePath, string branchName, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("show-ref");
        psi.ArgumentList.Add("--verify");
        psi.ArgumentList.Add("--quiet");
        psi.ArgumentList.Add($"refs/heads/{branchName}");

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return false; }

        if (proc is null) return false;

        using (proc)
        {
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0;
        }
    }

    public async Task<bool> IsBranchMergedIntoAsync(
        string workspacePath, string branchName, string baseBranch, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("branch");
        psi.ArgumentList.Add("--merged");
        psi.ArgumentList.Add(baseBranch);

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return false; }

        if (proc is null) return false;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0) return false;

            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimStart(' ', '*', '+').TrimEnd('\r', ' ');
                if (!string.Equals(trimmed, branchName, StringComparison.Ordinal))
                    continue;

                // Skip if branch tip is directly on the first-parent chain of base — unrun batch branch
                if (await IsBranchTipInFirstParentChainAsync(workspacePath, branchName, baseBranch, cancellationToken))
                    continue;

                return true;
            }
        }

        // git branch --merged misses squash-merges; git cherry detects them via patch-ID equivalence
        return await IsSquashMergedViaCherryAsync(workspacePath, branchName, baseBranch, cancellationToken);
    }

    private async Task<bool> IsSquashMergedViaCherryAsync(
        string workspacePath, string branchName, string baseBranch, CancellationToken cancellationToken)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("cherry");
        psi.ArgumentList.Add(baseBranch);
        psi.ArgumentList.Add(branchName);

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return false; }

        if (proc is null) return false;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0) return false;

            var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 && lines.All(l => l.StartsWith('-'));
        }
    }

    private async Task<int> GetRevListCountAsync(
        string workspacePath, string from, string to, CancellationToken cancellationToken)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("rev-list");
        psi.ArgumentList.Add("--count");
        psi.ArgumentList.Add($"{from}..{to}");

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return 0; }

        if (proc is null) return 0;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0) return 0;
            return int.TryParse(stdout.Trim(), out var count) ? count : 0;
        }
    }

    private async Task<bool> IsBranchTipInFirstParentChainAsync(
        string workspacePath, string branchName, string baseBranch, CancellationToken cancellationToken)
    {
        var tipSha = await GetRevParseAsync(workspacePath, branchName, cancellationToken);
        if (string.IsNullOrEmpty(tipSha))
            return false;

        // Resolve parent to bound the scan; initial commit (no parent) is trivially on the chain
        var parentSha = await GetRevParseAsync(workspacePath, branchName + "^", cancellationToken);
        if (string.IsNullOrEmpty(parentSha))
            return true;

        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("rev-list");
        psi.ArgumentList.Add("--first-parent");
        psi.ArgumentList.Add($"{parentSha}..{baseBranch}");

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return false; }

        if (proc is null) return false;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            if (proc.ExitCode != 0) return false;
            return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), tipSha, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<string> GetRevParseAsync(
        string workspacePath, string rev, CancellationToken cancellationToken)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("rev-parse");
        psi.ArgumentList.Add(rev);

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return string.Empty; }

        if (proc is null) return string.Empty;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0 ? stdout.Trim() : string.Empty;
        }
    }

    // Uses git for-each-ref rather than @{u} shorthand: @{u} relies on HEAD context resolution
    // which fails in Bishop's non-interactive process environment after a squash rebase + force-push.
    private async Task<string?> GetUpstreamRefAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var headPsi = CreateGitProcessStartInfo(workspacePath);
        headPsi.ArgumentList.Add("rev-parse");
        headPsi.ArgumentList.Add("--abbrev-ref");
        headPsi.ArgumentList.Add("HEAD");

        Process? headProc;
        try { headProc = Process.Start(headPsi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return null; }

        if (headProc is null) return null;

        string branchName;
        using (headProc)
        {
            var stdout = await headProc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await headProc.StandardError.ReadToEndAsync(cancellationToken);
            await headProc.WaitForExitAsync(cancellationToken);
            if (headProc.ExitCode != 0) return null;
            branchName = stdout.Trim();
            if (string.IsNullOrEmpty(branchName) || branchName == "HEAD") return null;
        }

        var upPsi = CreateGitProcessStartInfo(workspacePath);
        upPsi.ArgumentList.Add("for-each-ref");
        upPsi.ArgumentList.Add("--format=%(upstream:short)");
        upPsi.ArgumentList.Add($"refs/heads/{branchName}");

        Process? upProc;
        try { upProc = Process.Start(upPsi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return null; }

        if (upProc is null) return null;

        using (upProc)
        {
            var stdout = await upProc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await upProc.StandardError.ReadToEndAsync(cancellationToken);
            await upProc.WaitForExitAsync(cancellationToken);
            if (upProc.ExitCode != 0) return null;
            var upstream = stdout.Trim();
            return string.IsNullOrEmpty(upstream) ? null : upstream;
        }
    }

    public async Task<IReadOnlyList<string>> GetWorktreeBranchesAsync(
        string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("worktree");
        psi.ArgumentList.Add("list");
        psi.ArgumentList.Add("--porcelain");

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return []; }

        if (proc is null) return [];

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0) return [];

            const string prefix = "branch refs/heads/";
            var branches = new List<string>();
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    branches.Add(trimmed[prefix.Length..]);
            }
            return branches;
        }
    }

    public async Task<int?> GetBranchCommitCountAsync(
        string workspacePath, string branchName, string baseBranch, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("rev-list");
        psi.ArgumentList.Add("--count");
        psi.ArgumentList.Add($"{baseBranch}..{branchName}");

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return null; }

        if (proc is null) return null;

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0) return null;
            return int.TryParse(stdout.Trim(), out var count) ? count : null;
        }
    }

    public async Task StageAllAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("add");
        psi.ArgumentList.Add("-A");

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git add -A exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task<string> CommitAsync(string workspacePath, string message, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("commit");
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add(message);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git commit exited {proc.ExitCode}: {stderr.Trim()}");
        }

        var hashPsi = CreateGitProcessStartInfo(workspacePath);
        hashPsi.ArgumentList.Add("rev-parse");
        hashPsi.ArgumentList.Add("HEAD");

        proc = Process.Start(hashPsi);
        if (proc is null)
            throw new InvalidOperationException("Failed to start git process for rev-parse");

        using (proc)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git rev-parse HEAD exited {proc.ExitCode}: {stderr.Trim()}");

            return stdout.Trim();
        }
    }

    public async Task DeleteLocalBranchAsync(
        string workspacePath, string branchName, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("branch");
        psi.ArgumentList.Add("-D");
        psi.ArgumentList.Add(branchName);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        using (proc)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"git branch -D exited {proc.ExitCode}: {stderr.Trim()}");
        }
    }

    public async Task<MergeResult> MergeAsync(
        string workspacePath, string branchName, CancellationToken cancellationToken = default)
    {
        var psi = CreateGitProcessStartInfo(workspacePath);
        psi.ArgumentList.Add("merge");
        psi.ArgumentList.Add("--no-ff");
        psi.ArgumentList.Add(branchName);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git executable not found", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start git process");

        int exitCode;
        string stdout;
        string stderr;
        using (proc)
        {
            stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            exitCode = proc.ExitCode;
        }

        if (exitCode == 0)
            return new MergeResult(true, []);

        // Conflict — extract file list from merge output, then abort to restore clean state
        var conflictFiles = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("CONFLICT", StringComparison.Ordinal))
            .Select(line =>
            {
                var inIdx = line.IndexOf(" in ", StringComparison.Ordinal);
                return inIdx >= 0 ? line[(inIdx + 4)..].Trim() : line.Trim();
            })
            .ToList();

        if (conflictFiles.Count == 0)
            return new MergeResult(false, [], stderr.Trim());

        var abortPsi = CreateGitProcessStartInfo(workspacePath);
        abortPsi.ArgumentList.Add("merge");
        abortPsi.ArgumentList.Add("--abort");
        var abortProc = Process.Start(abortPsi);
        if (abortProc is not null)
        {
            using (abortProc)
                await abortProc.WaitForExitAsync(cancellationToken);
        }

        return new MergeResult(false, conflictFiles);
    }

    internal static ProcessStartInfo CreateGitProcessStartInfo(string workingDirectory)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return psi;
    }
}

using System.Diagnostics;

namespace Bishop.App.Batches;

/// <summary>
/// Shared helpers for the per-batch run lock written under a worktree's
/// <c>.bishop/</c> directory. The lock records the owning process id and a
/// timestamp (<c>{pid}\t{timestamp:O}</c>) so a run orphaned by a killed host
/// process can be detected (orphan reconciliation) and recovered
/// (<c>batch rescue</c>).
/// </summary>
internal static class BatchLock
{
    public static string LockFilePath(string worktreePath, Guid batchId) =>
        Path.Combine(worktreePath, ".bishop", $"batch-{batchId}.lock");

    /// <summary>
    /// Reads the owning process id recorded in a batch lock file. Returns
    /// <see langword="false"/> when the lock is absent, unreadable, or missing a
    /// parseable pid.
    /// </summary>
    public static bool TryReadOwnerPid(string worktreePath, Guid batchId, out int pid)
    {
        pid = 0;
        var lockPath = LockFilePath(worktreePath, batchId);
        if (!File.Exists(lockPath))
            return false;

        try
        {
            var parts = File.ReadAllText(lockPath).Split('\t');
            return parts.Length > 0 && int.TryParse(parts[0], out pid);
        }
        catch
        {
            // intentional: an unreadable lock is treated as having no resolvable owner
            return false;
        }
    }

    /// <summary>Whether a process with the given id is currently running.</summary>
    public static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            // intentional: GetProcessById throws when no process has the id; absence means not alive
            return false;
        }
    }

    /// <summary>
    /// Best-effort deletion of a lock file. Returns whether a file was actually
    /// removed (<see langword="false"/> when none existed or deletion failed).
    /// </summary>
    public static bool DeleteLockFile(string lockPath)
    {
        try
        {
            if (!File.Exists(lockPath))
                return false;
            File.Delete(lockPath);
            return true;
        }
        catch
        {
            // intentional: best-effort cleanup; a lingering lock is reconciled on the next run
            return false;
        }
    }
}

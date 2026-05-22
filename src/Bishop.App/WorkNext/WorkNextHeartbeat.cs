using System.Diagnostics;

namespace Bishop.App.WorkNext;

public sealed record WorkNextState(bool IsRunning, bool IsStopping);

public static class WorkNextHeartbeat
{
    public const string DirectoryName = ".bishop";
    public const string RunningFileName = "worknext.running";
    public const string StopFileName = "worknext.stop";

    public static WorkNextState ReadState(string bishopDirectoryPath, Func<int, bool>? pidIsAlive = null)
    {
        pidIsAlive ??= IsProcessAlive;
        var runningFile = Path.Combine(bishopDirectoryPath, RunningFileName);
        var stopFile = Path.Combine(bishopDirectoryPath, StopFileName);

        if (!File.Exists(runningFile))
            return new WorkNextState(false, false);

        int pid;
        try
        {
            var content = File.ReadAllText(runningFile);
            var firstLine = content.Split('\n', '\r')[0];
            if (!int.TryParse(firstLine, out pid))
            {
                TryDelete(runningFile);
                return new WorkNextState(false, false);
            }
        }
        catch (IOException)
        {
            // CLI is mid-write; assume running.
            return new WorkNextState(true, File.Exists(stopFile));
        }

        if (!pidIsAlive(pid))
        {
            TryDelete(runningFile);
            return new WorkNextState(false, false);
        }

        return new WorkNextState(true, File.Exists(stopFile));
    }

    public static bool IsProcessAlive(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

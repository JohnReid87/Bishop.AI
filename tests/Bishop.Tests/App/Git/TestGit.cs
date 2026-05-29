using System.Diagnostics;

namespace Bishop.Tests.App.Git;

/// <summary>
/// Shared launcher for the real <c>git</c> binary used to arrange fixtures in the
/// <c>App.Git</c> tests. Two guarantees the per-test ad-hoc launchers lacked:
///
/// <list type="bullet">
/// <item><b>Hermetic</b> — global and system git config are neutralised, so host settings
/// (<c>commit.gpgsign</c>, <c>credential.helper</c>, <c>core.hooksPath</c>, pagers, …) cannot
/// reach these subprocesses and turn an <c>init</c>/<c>commit</c>/<c>push</c> into a blocking
/// prompt. <c>GIT_TERMINAL_PROMPT=0</c> makes any prompt that slips through fail fast.</item>
/// <item><b>Bounded</b> — every invocation has a hard timeout; on expiry the process tree is
/// killed and a <see cref="TimeoutException"/> is thrown, so a misconfigured environment fails
/// in seconds with a clear message instead of hanging the test run for minutes.</item>
/// </list>
/// </summary>
internal static class TestGit
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // An empty config file that GIT_CONFIG_GLOBAL/SYSTEM point at; created once per test run.
    private static readonly string EmptyConfigPath = CreateEmptyConfig();

    /// <summary>Runs git, discarding stdout. Throws on timeout.</summary>
    public static void Run(string workingDirectory, params string[] args) =>
        Execute(workingDirectory, args, captureOutput: false);

    /// <summary>Runs git and returns trimmed stdout. Throws on timeout.</summary>
    public static string Capture(string workingDirectory, params string[] args) =>
        Execute(workingDirectory, args, captureOutput: true).Trim();

    private static string Execute(string workingDirectory, string[] args, bool captureOutput)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            WorkingDirectory = workingDirectory,
        };
        psi.Environment["GIT_CONFIG_GLOBAL"] = EmptyConfigPath;
        psi.Environment["GIT_CONFIG_SYSTEM"] = EmptyConfigPath;
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;

        // Drain stdout asynchronously so a full pipe buffer can never deadlock the bounded wait.
        var outputTask = captureOutput ? proc.StandardOutput.ReadToEndAsync() : null;

        if (!proc.WaitForExit((int)DefaultTimeout.TotalMilliseconds))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException(
                $"git {string.Join(' ', args)} did not exit within {DefaultTimeout.TotalSeconds:0}s " +
                $"(working dir: {workingDirectory}). This usually means a host git setting forced an " +
                "interactive prompt.");
        }

        // Ensure async stdout reader has flushed now that the process has exited.
        proc.WaitForExit();
        return outputTask?.GetAwaiter().GetResult() ?? string.Empty;
    }

    private static string CreateEmptyConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "bishop-tests-empty.gitconfig");
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);
        return path;
    }
}

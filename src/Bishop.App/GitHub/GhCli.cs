using System.Diagnostics;

namespace Bishop.App.GitHub;

public sealed class GhCli : IGhCli
{
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("gh") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi);
        if (proc is not null) await proc.WaitForExitAsync(cancellationToken);
    }

    public async Task<string> RunCaptureAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("gh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start gh process.");
        var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"gh exited {proc.ExitCode}: {stderr.Trim()}");
        return stdout.Trim();
    }
}

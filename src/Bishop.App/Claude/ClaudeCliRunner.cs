using System.ComponentModel;
using System.Diagnostics;

namespace Bishop.App.Claude;

public sealed class ClaudeCliRunner : IClaudeCliRunner
{
    public async Task<int> RunPromptAsync(
        string workspacePath,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo("claude")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspacePath,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                "Could not start 'claude' — is the Claude Code CLI installed and on PATH?", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start 'claude' process.");

        using (proc)
        {
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) Console.Out.WriteLine(e.Data);
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) Console.Error.WriteLine(e.Data);
            };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode;
        }
    }
}

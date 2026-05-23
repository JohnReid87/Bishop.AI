using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Spectre.Console;

namespace Bishop.App.Services.Claude;

public sealed class ClaudeCliRunner : IClaudeCliRunner
{
    private const string InstallUrl = "https://docs.claude.com/en/docs/claude-code/setup";

    private readonly IClaudeExecutableResolver _resolver;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;

    public ClaudeCliRunner(IClaudeExecutableResolver resolver)
        : this(resolver, Process.Start) { }

    public ClaudeCliRunner(IClaudeExecutableResolver resolver, Func<ProcessStartInfo, Process?> processStarter)
    {
        _resolver = resolver;
        _processStarter = processStarter;
    }

    public async Task<ClaudeRunResult> RunPromptAsync(
        string workspacePath,
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        string claudePath;
        try
        {
            claudePath = _resolver.Resolve();
        }
        catch (ClaudeNotFoundException ex)
        {
            throw new InvalidOperationException(BuildNotFoundMessage(ex), ex);
        }

        var psi = new ProcessStartInfo(claudePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspacePath,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--verbose");
        if (model is not null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(model);
        }

        Process? proc;
        try
        {
            proc = _processStarter(psi);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Could not start 'claude' from '{claudePath}'. See {InstallUrl}", ex);
        }

        if (proc is null)
            throw new InvalidOperationException("Failed to start 'claude' process.");

        using (proc)
        {
            ClaudeRunTotals? totals = null;
            var toolUseCount = 0;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Working...", async ctx =>
                {
                    var formatter = new StreamJsonFormatter(label => ctx.Status(label));
                    proc.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data is null) return;
                        var formatted = formatter.Format(e.Data);
                        if (formatted is not null) AnsiConsole.WriteLine(formatted);
                    };
                    proc.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data is not null) AnsiConsole.WriteLine(e.Data);
                    };

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await proc.WaitForExitAsync(cancellationToken);

                    totals = formatter.Totals;
                    toolUseCount = formatter.ToolUseCount;
                });

            return new ClaudeRunResult(proc.ExitCode, totals, toolUseCount);
        }
    }

    private static string BuildNotFoundMessage(ClaudeNotFoundException ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Could not find 'claude' on PATH.");
        sb.AppendLine($"Tried: {string.Join(", ", ex.Candidates)}");
        sb.AppendLine("Searched directories:");
        if (ex.Directories.Count == 0)
        {
            sb.AppendLine("  (PATH was empty)");
        }
        else
        {
            foreach (var dir in ex.Directories)
                sb.AppendLine($"  {dir}");
        }
        sb.Append($"Install Claude Code: {InstallUrl}");
        return sb.ToString();
    }
}

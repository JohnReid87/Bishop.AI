using Bishop.App.Skills;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Bishop.App.Services.Claude;

internal sealed class ClaudeCliRunner : IClaudeCliRunner
{
    private const string InstallUrl = "https://docs.claude.com/en/docs/claude-code/setup";
    internal const int TranscriptRetentionCount = 10;
    internal const int DenialsMaxLines = 500;

    private static readonly string[] DefaultPathExt = [".COM", ".EXE", ".BAT", ".CMD"];

    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly TimeProvider _timeProvider;
    private readonly string? _explicitPath;
    private readonly object _resolvedPathGate = new();
    private string? _resolvedPath;

    public ClaudeCliRunner(TimeProvider timeProvider)
        : this(Process.Start, timeProvider, claudePath: null) { }

    public ClaudeCliRunner(
        Func<ProcessStartInfo, Process?> processStarter,
        TimeProvider timeProvider,
        string? claudePath = null)
    {
        _processStarter = processStarter;
        _timeProvider = timeProvider;
        _explicitPath = claudePath;
    }

    public async Task<ClaudeRunResult> RunPromptAsync(
        string workspacePath,
        string prompt,
        string model = SkillModelOptions.DefaultModelId,
        int? cardNumber = null,
        string? transcriptBasePath = null,
        CancellationToken cancellationToken = default)
    {
        string claudePath;
        try
        {
            claudePath = _explicitPath ?? ResolveAndCache();
        }
        catch (ClaudeNotFoundException ex)
        {
            throw new InvalidOperationException(BuildNotFoundMessage(ex), ex);
        }

        var psi = new ProcessStartInfo(claudePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            StandardInputEncoding = Encoding.UTF8,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspacePath,
        };
        psi.EnvironmentVariables["BISHOP_AUTO_CARD"] = "1";
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--verbose");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);

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

        string? transcriptPath = null;
        if (cardNumber.HasValue)
        {
            var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMddTHHmmss");
            var runsDir = Path.Combine(transcriptBasePath ?? workspacePath, ".bishop", "runs");
            Directory.CreateDirectory(runsDir);
            PruneTranscripts(runsDir, cardNumber.Value);
            transcriptPath = Path.Combine(runsDir, $"{cardNumber}-{stamp}Z.jsonl");
        }

        using (proc)
        {
            ClaudeRunTotals? totals = null;
            var toolUseCount = 0;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Working...", async ctx =>
                {
                    var formatter = new StreamJsonFormatter(
                        onStatus: label => ctx.Status(Markup.Escape(label)),
                        onDenial: ev => AppendDenial(workspacePath, cardNumber, ev));
                    proc.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data is null) return;
                        var formatted = formatter.Format(e.Data);
                        if (formatted is not null) AnsiConsole.WriteLine(formatted);
                        if (transcriptPath is not null)
                            WriteTranscriptLine(transcriptPath, e.Data);
                    };
                    proc.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data is not null) AnsiConsole.WriteLine(e.Data);
                    };

                    try
                    {
                        await proc.StandardInput.WriteAsync(prompt);
                        proc.StandardInput.Close();
                    }
                    catch (IOException) { } // process exited before reading stdin

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await proc.WaitForExitAsync(cancellationToken);

                    totals = formatter.Totals;
                    toolUseCount = formatter.ToolUseCount;
                });

            return new ClaudeRunResult(proc.ExitCode, totals, toolUseCount, transcriptPath);
        }
    }

    private string ResolveAndCache()
    {
        if (_resolvedPath is not null) return _resolvedPath;
        lock (_resolvedPathGate)
        {
            _resolvedPath ??= ResolveClaudePath();
            return _resolvedPath;
        }
    }

    internal static string ResolveClaudePath() =>
        ResolveClaudePath(Environment.GetEnvironmentVariable, File.Exists, OperatingSystem.IsWindows());

    internal static string ResolveClaudePath(
        Func<string, string?> getEnv,
        Func<string, bool> fileExists,
        bool isWindows)
    {
        var directories = ReadPathDirectories(getEnv);
        var candidates = BuildCandidateNames(getEnv, isWindows);

        foreach (var dir in directories)
        {
            foreach (var name in candidates)
            {
                var full = Path.Combine(dir, name);
                if (fileExists(full))
                    return full;
            }
        }

        throw new ClaudeNotFoundException(candidates, directories);
    }

    private static IReadOnlyList<string> ReadPathDirectories(Func<string, string?> getEnv)
    {
        var pathEnv = getEnv("PATH") ?? string.Empty;
        return pathEnv
            .Split(Path.PathSeparator)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildCandidateNames(Func<string, string?> getEnv, bool isWindows)
    {
        if (!isWindows) return ["claude"];

        var pathExt = getEnv("PATHEXT");
        var extensions = string.IsNullOrWhiteSpace(pathExt)
            ? DefaultPathExt
            : pathExt
                .Split(';')
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToArray();

        return extensions.Select(e => "claude" + e).ToArray();
    }

    private static readonly JsonSerializerOptions DenialSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // OutputDataReceived fires on thread-pool threads; serialize appends so concurrent
    // writes cannot interleave bytes and corrupt the JSONL files.
    private readonly object DenialFileGate = new();
    private readonly object TranscriptFileGate = new();

    private void AppendDenial(string workspacePath, int? cardNumber, PermissionDeniedEvent ev)
    {
        var bishopDir = Path.Combine(workspacePath, ".bishop");
        Directory.CreateDirectory(bishopDir);
        var entry = new DenialLogEntry(
            _timeProvider.GetUtcNow().ToString("o"),
            cardNumber,
            ev.Tool,
            ev.Command,
            ev.Message);
        var json = JsonSerializer.Serialize(entry, DenialSerializerOptions);
        WriteDenialLine(Path.Combine(bishopDir, "denials.jsonl"), json);
    }

    internal void WriteDenialLine(string filePath, string json)
    {
        lock (DenialFileGate)
        {
            File.AppendAllText(filePath, json + "\n");
            TrimDenialsIfNeeded(filePath);
        }
    }

    internal static void TrimDenialsIfNeeded(string filePath)
    {
        var lines = File.ReadAllLines(filePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        if (lines.Length <= DenialsMaxLines) return;
        File.WriteAllText(filePath, string.Join("\n", lines[^DenialsMaxLines..]) + "\n");
    }

    internal static void PruneTranscripts(string runsDir, int cardNumber, int retentionCount = TranscriptRetentionCount)
    {
        var toDelete = Directory.GetFiles(runsDir, $"{cardNumber}-*Z.jsonl")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .Skip(retentionCount - 1)
            .ToList();
        foreach (var file in toDelete)
            File.Delete(file);
    }

    internal void WriteTranscriptLine(string filePath, string line)
    {
        lock (TranscriptFileGate)
        {
            File.AppendAllText(filePath, line + "\n");
        }
    }

    private sealed record DenialLogEntry(
        string Timestamp,
        int? CardNumber,
        string? Tool,
        string? Command,
        string? Message);

    internal static string BuildNotFoundMessage(ClaudeNotFoundException ex)
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

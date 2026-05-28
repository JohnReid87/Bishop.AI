using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bishop.Cli.Hooks.CheckPath;

internal sealed class CheckPathCliCommand : Command
{
    private static readonly JsonSerializerOptions LogOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public CheckPathCliCommand(string? workspacePathOverride = null)
        : base("check-path", "PreToolUse hook: block Edit/Write/NotebookEdit targeting paths outside the workspace")
    {
        this.SetHandler(async (context) =>
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BISHOP_AUTO_CARD")))
                return;

            var payload = await Console.In.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(payload))
                return;

            JsonNode? root;
            try { root = JsonNode.Parse(payload); }
            catch { return; }

            var toolName = root?["tool_name"]?.GetValue<string>();
            if (toolName is not ("Edit" or "Write" or "NotebookEdit"))
                return;

            var toolInput = root?["tool_input"];
            var targetPath = toolName == "NotebookEdit"
                ? toolInput?["notebook_path"]?.GetValue<string>()
                : toolInput?["file_path"]?.GetValue<string>();

            if (string.IsNullOrEmpty(targetPath))
                return;

            var workspacePath = workspacePathOverride ?? Directory.GetCurrentDirectory();
            var normalizedTarget = Path.GetFullPath(targetPath);
            var normalizedWorkspace = Path.GetFullPath(workspacePath);

            if (normalizedTarget.StartsWith(normalizedWorkspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedTarget.Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
                return;

            AppendDenial(workspacePath, toolName, targetPath);
            Console.Error.WriteLine($"bishop hook check-path: blocked {toolName} -> {targetPath}");
            context.ExitCode = 1;
        });
    }

    private static void AppendDenial(string workspacePath, string tool, string path)
    {
        var bishopDir = Path.Combine(workspacePath, ".bishop");
        Directory.CreateDirectory(bishopDir);
        var entry = new DenialLogEntry(
            DateTimeOffset.UtcNow.ToString("o"),
            null,
            tool,
            path,
            "Path outside workspace");
        var json = JsonSerializer.Serialize(entry, LogOptions);
        File.AppendAllText(Path.Combine(bishopDir, "denials.jsonl"), json + "\n");
    }

    private sealed record DenialLogEntry(
        string Timestamp,
        int? CardNumber,
        string? Tool,
        string? Command,
        string? Message);
}

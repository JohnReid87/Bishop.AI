using System.Text.Json.Nodes;

namespace Bishop.Life.Core;

/// <summary>
/// Tails a Claude Code session JSONL file as it grows on disk and raises
/// typed events for the three things the bishop.life stand-up transcript
/// renders: user prose, assistant text, and tool-use ghost lines. Tool
/// results are deliberately ignored — the transcript surfaces only what the
/// user needs to read.
/// </summary>
/// <remarks>
/// JSONL file may not exist yet at <see cref="Start"/>; the watcher fires on
/// <c>Created</c>. Reads happen from a tracked byte offset so each appended
/// line is processed exactly once. The file is opened with full sharing so
/// claude's append stream is never blocked.
/// </remarks>
public sealed class ClaudeSessionJsonlTailer : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(75);

    private readonly string _filePath;
    private readonly TimeSpan _debounce;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private long _position;
    private string _pendingPartial = string.Empty;
    private bool _disposed;
    private bool _pending;

    public event Action<string>? UserMessage;
    public event Action<string>? AssistantText;
    public event Action<ToolUseEvent>? ToolUse;

    public ClaudeSessionJsonlTailer(string filePath) : this(filePath, DefaultDebounce) { }

    public ClaudeSessionJsonlTailer(string filePath, TimeSpan debounce)
    {
        _filePath = filePath;
        _debounce = debounce;

        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("File path has no directory.", nameof(filePath));
        Directory.CreateDirectory(directory);

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Created += OnFsEvent;
        _watcher.Changed += OnFsEvent;
        _watcher.Renamed += OnFsEvent;

        _timer = new System.Threading.Timer(Fire, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        // Pump once in case the file already exists (e.g. tailing a resumed session).
        ScheduleRead();
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e) => ScheduleRead();

    private void ScheduleRead()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = true;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Fire(object? _)
    {
        bool shouldFire;
        lock (_gate)
        {
            shouldFire = _pending && !_disposed;
            _pending = false;
        }
        if (shouldFire) PumpOnce();
    }

    private void PumpOnce()
    {
        if (!File.Exists(_filePath)) return;

        string buffer;
        try
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            // Handle truncation (rare — session files only append) by resetting.
            if (fs.Length < _position)
            {
                _position = 0;
                _pendingPartial = string.Empty;
            }
            if (fs.Length == _position) return;

            fs.Seek(_position, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            buffer = reader.ReadToEnd();
            _position = fs.Position;
        }
        catch (IOException)
        {
            // Writer mid-flush; re-arm so we try again on the next FS event.
            ScheduleRead();
            return;
        }

        var combined = _pendingPartial + buffer;
        var lastNewline = combined.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            _pendingPartial = combined;
            return;
        }
        var complete = combined[..lastNewline];
        _pendingPartial = combined[(lastNewline + 1)..];

        foreach (var line in complete.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            try { ProcessLine(trimmed); }
            catch (Exception ex)
            {
                // One bad line shouldn't stop the tail — claude's JSONL format
                // is stable, so a parse error here is genuinely unexpected
                // (truncated mid-write should be impossible thanks to the
                // trailing-newline split above). Log + continue.
                System.Diagnostics.Debug.WriteLine($"ClaudeSessionJsonlTailer: dropped line — {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Visible for testing — parses a single JSONL line and raises the matching
    /// event(s). Unknown shapes are dropped silently.
    /// </summary>
    internal void ProcessLine(string line)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(line); }
        catch { return; }
        if (node is null) return;

        var type = node["type"]?.GetValue<string>();
        if (type == "user")
        {
            // Skip tool_result envelopes — they arrive as type:"user" but their
            // content array contains tool_result entries, not human prose.
            if (HasToolResult(node["message"])) return;
            var text = StripMetaTags(ExtractTextContent(node["message"]));
            if (!string.IsNullOrWhiteSpace(text)) UserMessage?.Invoke(text);
        }
        else if (type == "assistant")
        {
            EmitAssistantBlocks(node["message"]);
        }
    }

    private void EmitAssistantBlocks(JsonNode? message)
    {
        if (message?["content"] is not JsonArray arr) return;
        foreach (var item in arr)
        {
            var kind = item?["type"]?.GetValue<string>();
            if (kind == "text")
            {
                var t = item!["text"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(t)) AssistantText?.Invoke(t!);
            }
            else if (kind == "tool_use")
            {
                var name = item!["name"]?.GetValue<string>() ?? "";
                var summary = SummariseToolUse(name, item["input"]);
                ToolUse?.Invoke(new ToolUseEvent(name, summary));
            }
        }
    }

    private static bool HasToolResult(JsonNode? message)
    {
        if (message?["content"] is not JsonArray arr) return false;
        foreach (var item in arr)
            if (item?["type"]?.GetValue<string>() == "tool_result") return true;
        return false;
    }

    private static string ExtractTextContent(JsonNode? message)
    {
        if (message is null) return string.Empty;
        var content = message["content"];
        if (content is JsonArray arr)
        {
            var parts = new List<string>();
            foreach (var item in arr)
            {
                if (item?["type"]?.GetValue<string>() == "text")
                {
                    var t = item["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(t)) parts.Add(t);
                }
            }
            return string.Join("\n", parts);
        }
        if (content is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return string.Empty;
    }

    /// <summary>
    /// Visible for testing. Strips Claude Code's meta-tag noise (system
    /// reminders, command envelopes) so they never appear as "user messages"
    /// in the transcript. If the entire payload was meta, returns empty.
    /// </summary>
    internal static string StripMetaTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var s = System.Text.RegularExpressions.Regex.Replace(
            text, @"<system-reminder>.*?</system-reminder>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);
        s = System.Text.RegularExpressions.Regex.Replace(
            s, @"<command-(name|message|args)>.*?</command-\1>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);
        return s.Trim();
    }

    /// <summary>
    /// Visible for testing. Produces the single-line ghost description shown
    /// in the transcript for a tool_use block. Falls back to the bare tool
    /// name when the input shape is unfamiliar.
    /// </summary>
    internal static string SummariseToolUse(string name, JsonNode? input)
    {
        string? Get(string key) => input?[key]?.GetValue<string>();
        var lower = name.ToLowerInvariant();
        return lower switch
        {
            "read"        => $"reading {Truncate(Get("file_path") ?? Get("path") ?? "")}",
            "write"       => $"writing {Truncate(Get("file_path") ?? Get("path") ?? "")}",
            "edit"        => $"editing {Truncate(Get("file_path") ?? Get("path") ?? "")}",
            "notebookedit" => $"editing {Truncate(Get("notebook_path") ?? "")}",
            "glob"        => $"globbing {Truncate(Get("pattern") ?? "")}",
            "grep"        => $"grepping {Truncate(Get("pattern") ?? "")}",
            "bash"        => $"running {Truncate(Get("command") ?? "")}",
            "powershell"  => $"running {Truncate(Get("command") ?? "")}",
            "webfetch"    => $"fetching {Truncate(Get("url") ?? "")}",
            "websearch"   => $"searching {Truncate(Get("query") ?? "")}",
            "todowrite"   => "updating todos",
            "task"        => $"spawning agent: {Truncate(Get("description") ?? "")}",
            _             => name
        };
    }

    private static string Truncate(string s, int max = 80)
    {
        s = (s ?? "").Trim();
        if (s.Length <= max) return s;
        return s[..(max - 1)] + "…";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFsEvent;
        _watcher.Changed -= OnFsEvent;
        _watcher.Renamed -= OnFsEvent;
        _watcher.Dispose();
        _timer.Dispose();
    }

    public readonly record struct ToolUseEvent(string Name, string Summary);
}

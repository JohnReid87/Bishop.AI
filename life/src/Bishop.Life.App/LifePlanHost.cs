using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bishop.Life.App.Speak;
using Bishop.Life.App.Web;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI;

namespace Bishop.Life.App;

/// <summary>
/// Wires a <see cref="WebView2"/> to a bishop.life JSON file: loads the asset HTML
/// over a virtual host mapping, reads the plan via <see cref="LifePlanFileService"/>,
/// and posts envelope state to JS. State is refreshed from disk on every debounced
/// disk change via <see cref="LifePlanWatcher"/>, with <see cref="NoteWindowActivated"/>
/// as a belt-and-braces refresh for cases where the watcher is late or the user
/// returns from a launched terminal that wrote nothing.
/// Handles these JS→host message shapes:
/// <list type="bullet">
///   <item><c>"standup"</c> — tries the embedded ConPTY terminal first (card
///         #1053), falling back to <c>wt.exe</c> if Pty.Net spawn fails.</item>
///   <item><c>{"type":"mutate","plan":{…}}</c> — applies an inline edit (star,
///         check, title) via <see cref="LifeMutationCoordinator"/>.</item>
///   <item><c>{"type":"terminal:input","data":"…"}</c> — keystroke from xterm.js
///         to forward into the PTY stdin.</item>
///   <item><c>{"type":"terminal:resize","cols":n,"rows":m}</c> — viewport size
///         change so ConPTY's screen buffer tracks the rendered grid.</item>
/// </list>
/// Posts <c>{"type":"terminal:show|data|hide|systemNote"}</c> back to JS to
/// drive the xterm.js pane lifecycle. <c>systemNote</c> renders a muted
/// in-transcript bubble (card #1065) — used to surface input that was
/// dropped between JS and the PTY, and session-end races with input.
/// </summary>
internal sealed class LifePlanHost : IDisposable
{
    private const string VirtualHost = "bishop.life";
    private const string LandingUrl = "https://bishop.life/index.html";
    private const string StandupCommand = "/bish-life-standup";
    private const string InitCommand = "/bish-life-init";
    private const string AddCommand = "/bish-life-add";

    private static readonly Color DarkBackground = Color.FromArgb(255, 0x14, 0x14, 0x14);
    private static readonly Color LightBackground = Color.FromArgb(255, 0xF3, 0xF3, 0xF3);

    private readonly WebView2 _view;
    private readonly DispatcherQueue _dispatcher;
    private readonly LifePlanFileService _service;
    private readonly LifePlanWatcher _watcher;
    private readonly LifeTerminalLauncher _launcher;
    private readonly LifeMutationCoordinator _coordinator;
    private SpeakController? _speak;
    private ClaudePtySession? _standupPty;
    private ClaudeSessionJsonlTailer? _transcriptTailer;
    private bool _navigated;
    private bool _disposed;
    // Default xterm.js geometry on first show; resized as soon as the JS side
    // measures its container and posts terminal:resize. 80×30 is the wt.exe
    // default so the first frame matches what users see today.
    private int _ptyCols = 80;
    private int _ptyRows = 30;
    // Set by MainWindow before EnsureCoreWebView2Async completes so the very
    // first paint of the WebView2 uses the right theme.
    private bool _pendingDarkTheme = true;

    private static readonly JsonSerializerOptions PostOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public LifePlanHost(WebView2 view)
    {
        _view = view;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _service = new LifePlanFileService();
        _watcher = new LifePlanWatcher(_service.FilePath);
        _watcher.Reloaded += OnFileReloaded;
        _launcher = new LifeTerminalLauncher();
        _coordinator = new LifeMutationCoordinator(_service);
        _coordinator.StateChanged += OnCoordinatorStateChanged;
    }

    public async Task StartAsync()
    {
        // Suppress the white flash that WebView2 paints before the first HTML
        // frame lands. Has to land before EnsureCoreWebView2Async or the bare
        // CoreWebView2 surface paints white for a frame on first show.
        _view.DefaultBackgroundColor = _pendingDarkTheme ? DarkBackground : LightBackground;

        await _view.EnsureCoreWebView2Async();

        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _view.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);

        _view.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _view.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _view.CoreWebView2.Navigate(LandingUrl);

        _watcher.Start();

        var channel = new WebView2BrowserChannel(_view.CoreWebView2, _dispatcher);
        _speak = SpeakController.Create(channel);
        _speak.Start();
    }

    /// <summary>
    /// Called by the host window on activation. Pushes a fresh envelope so the
    /// WebView2 reflects current disk state — covers init completion (file went
    /// missing→ok), stand-up completion (file rewritten), and stand-up abort
    /// (terminal closed without a write). Also clears any stuck in-flight flag.
    /// </summary>
    public void NoteWindowActivated()
    {
        if (!_navigated || _disposed) return;
        var cleared = false;
        if (_coordinator.StandupInFlight)
        {
            _coordinator.NoteStandupAborted(); // fires StateChanged → PostState
            cleared = true;
        }
        if (_coordinator.AddInFlight)
        {
            _coordinator.NoteAddAborted();
            cleared = true;
        }
        if (!cleared) PostState();
    }

    public void SetTheme(bool isDark)
    {
        _pendingDarkTheme = isDark;
        if (_view.CoreWebView2 is null) return;
        _view.DefaultBackgroundColor = isDark ? DarkBackground : LightBackground;
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // WebView2 raises this on the UI thread. Messages arrive in two shapes —
        // a bare "standup" string (legacy path) or a JSON object {type,plan}.
        var json = args.WebMessageAsJson; // always JSON-encoded, even for strings
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                var s = root.GetString();
                if (s == "standup") LaunchStandup();
                else if (s == "init") LaunchInit();
                else if (s == "add") LaunchAdd();
                return;
            }

            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();

            if (type == "standup")
            {
                LaunchStandup();
            }
            else if (type == "init")
            {
                LaunchInit();
            }
            else if (type == "add")
            {
                LaunchAdd();
            }
            else if (type == "mutate" && root.TryGetProperty("plan", out var planEl))
            {
                ApplyMutation(planEl);
            }
            else if (type == "terminal:input" && root.TryGetProperty("data", out var dataEl))
            {
                // Card #1065: surface dropped input. Four prior fixes (#1061–#1064)
                // worked blind because the silent failure modes (PTY not attached,
                // Write throws, session exited) all looked identical from the UI:
                // nothing happens. Each branch posts a distinct system-note bubble
                // so the next repro names the cause.
                var input = dataEl.GetString() ?? string.Empty;
                var pty = _standupPty;
                if (pty is null)
                {
                    Debug.WriteLine("LifePlanHost: terminal:input dropped — PTY not attached");
                    PostTerminalSystemNote("[input dropped — PTY not attached]");
                }
                else
                {
                    try
                    {
                        pty.Write(input);
                        Debug.WriteLine($"LifePlanHost: terminal:input wrote {input.Length} chars");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LifePlanHost: terminal:input Write threw: {ex}");
                        PostTerminalSystemNote($"[input dropped — Write threw: {ex.Message}]");
                    }
                }
            }
            else if (type == "terminal:resize")
            {
                if (root.TryGetProperty("cols", out var colsEl) && colsEl.TryGetInt32(out var c)) _ptyCols = c;
                if (root.TryGetProperty("rows", out var rowsEl) && rowsEl.TryGetInt32(out var r)) _ptyRows = r;
                _standupPty?.Resize(_ptyCols, _ptyRows);
            }
        }
        catch (JsonException ex)
        {
            // JS only ever sends well-formed envelopes; if this fires the bridge
            // contract has drifted. Log so it shows up in Debug Output, then drop.
            Debug.WriteLine($"LifePlanHost: malformed web message dropped: {ex.Message}");
        }
    }

    private void LaunchStandup()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder); // ensure cwd target exists on first run

        // Card #1059: pin a session id up front so the JSONL tailer can find the
        // on-disk transcript without a race against claude's first write.
        var sessionId = Guid.NewGuid().ToString();
        var claudeArgs = $"{StandupCommand} --session-id {sessionId}";

        // Card #1053: try the embedded ConPTY pane first so the stand-up renders
        // inside Bishop.Life. Any failure (Pty.Net throw, missing claude,
        // ConPTY unavailable) falls through to the legacy wt.exe path so the
        // user still gets a working stand-up.
        var pty = _launcher.TryLaunchClaudePty(folder, claudeArgs, _ptyCols, _ptyRows);
        if (pty is not null)
        {
            AttachStandupPty(pty);
            AttachTranscriptTailer(folder, sessionId);
            PostTerminalShow();
        }
        else
        {
            _launcher.LaunchClaude(folder, claudeArgs);
        }
        _coordinator.NoteStandupLaunched(); // fires StateChanged → PostState
    }

    private void AttachTranscriptTailer(string cwd, string sessionId)
    {
        DetachTranscriptTailer();
        try
        {
            var jsonlPath = ClaudeSessionPaths.ResolveSessionFilePath(cwd, sessionId);
            var tailer = new ClaudeSessionJsonlTailer(jsonlPath);
            tailer.UserMessage += OnTranscriptUser;
            tailer.AssistantText += OnTranscriptAssistant;
            tailer.ToolUse += OnTranscriptTool;
            tailer.Start();
            _transcriptTailer = tailer;
        }
        catch (Exception ex)
        {
            // Tailer is best-effort: stand-up still works via terminal:data if it
            // ever gets re-enabled in the JS. Don't propagate.
            Debug.WriteLine($"LifePlanHost: transcript tailer failed: {ex.Message}");
        }
    }

    private void DetachTranscriptTailer()
    {
        if (_transcriptTailer is null) return;
        _transcriptTailer.UserMessage -= OnTranscriptUser;
        _transcriptTailer.AssistantText -= OnTranscriptAssistant;
        _transcriptTailer.ToolUse -= OnTranscriptTool;
        _transcriptTailer.Dispose();
        _transcriptTailer = null;
    }

    private void OnTranscriptUser(string text) =>
        _dispatcher.TryEnqueue(() => PostTranscriptEvent("user", text));

    private void OnTranscriptAssistant(string text) =>
        _dispatcher.TryEnqueue(() => PostTranscriptEvent("assistant", text));

    private void OnTranscriptTool(ClaudeSessionJsonlTailer.ToolUseEvent evt) =>
        _dispatcher.TryEnqueue(() => PostTranscriptEvent("tool", evt.Summary));

    private void PostTranscriptEvent(string kind, string text)
    {
        if (!_navigated || _disposed) return;
        var payload = new TranscriptEventEnvelope(Type: "transcript:event", Kind: kind, Text: text);
        var json = JsonSerializer.Serialize(payload, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void AttachStandupPty(ClaudePtySession pty)
    {
        DetachStandupPty(); // belt-and-braces if a prior session lingered
        _standupPty = pty;
        pty.DataReceived += OnStandupPtyData;
        pty.ProcessExited += OnStandupPtyExited;
    }

    private void DetachStandupPty()
    {
        if (_standupPty is null) return;
        _standupPty.DataReceived -= OnStandupPtyData;
        _standupPty.ProcessExited -= OnStandupPtyExited;
        _standupPty.Dispose();
        _standupPty = null;
    }

    private void OnStandupPtyData(string data) =>
        _dispatcher.TryEnqueue(() => PostTerminalData(data));

    private void OnStandupPtyExited()
    {
        _dispatcher.TryEnqueue(() =>
        {
            DetachStandupPty();
            DetachTranscriptTailer();
            // Card #1065: surface the session-ended cause BEFORE hiding the pane,
            // and delay the hide so the note is visible long enough to read when
            // it races with a user keystroke (the abyss-bug repro path).
            PostTerminalSystemNote("[Claude session ended]");
            if (_coordinator.StandupInFlight) _coordinator.NoteStandupAborted();
            else PostState();

            var timer = _dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1500);
            timer.IsRepeating = false;
            timer.Tick += (s, _) =>
            {
                s.Stop();
                PostTerminalHide();
            };
            timer.Start();
        });
    }

    private void PostTerminalShow() => PostBareEnvelope("terminal:show");
    private void PostTerminalHide() => PostBareEnvelope("terminal:hide");

    private void PostBareEnvelope(string type)
    {
        if (!_navigated || _disposed) return;
        _view.CoreWebView2.PostWebMessageAsJson($"{{\"type\":\"{type}\"}}");
    }

    private void PostTerminalData(string data)
    {
        if (!_navigated || _disposed) return;
        var payload = new TerminalDataEnvelope(Type: "terminal:data", Data: data);
        var json = JsonSerializer.Serialize(payload, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void PostTerminalSystemNote(string text)
    {
        if (!_navigated || _disposed) return;
        var payload = new TerminalSystemNoteEnvelope(Type: "terminal:systemNote", Text: text);
        var json = JsonSerializer.Serialize(payload, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void LaunchInit()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);

        _launcher.LaunchClaude(folder, InitCommand);
        // Init seeds the file from scratch; window activation on return refreshes
        // the envelope.
    }

    private void LaunchAdd()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);

        _launcher.LaunchClaude(folder, AddCommand);
        _coordinator.NoteAddLaunched(); // fires StateChanged → PostState
    }

    private void ApplyMutation(JsonElement planEl)
    {
        LifePlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<LifePlan>(planEl.GetRawText(), PostOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"LifePlanHost: malformed mutate payload dropped: {ex.Message}");
            return;
        }
        if (plan is null) return;

        _coordinator.ApplyMutation(plan);
        PostState();
    }

    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _navigated = true;
        PostState();
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(PostState);
    }

    /// <summary>
    /// Watcher fired — disk changed. The JS layer blocks inline mutations while a
    /// stand-up or add is in flight, so any reload event during in-flight is from
    /// the launched terminal (init seed, stand-up rewrite, or add append) and
    /// clears the flags. On the UI thread because <see cref="LifeMutationCoordinator"/>
    /// isn't thread-safe.
    /// </summary>
    private void OnFileReloaded(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_disposed) return;
            var cleared = false;
            if (_coordinator.StandupInFlight)
            {
                _coordinator.NoteStandupAborted(); // fires StateChanged → PostState
                cleared = true;
            }
            if (_coordinator.AddInFlight)
            {
                _coordinator.NoteAddAborted();
                cleared = true;
            }
            if (!cleared) PostState();
        });
    }

    private void PostState()
    {
        if (!_navigated || _disposed) return;

        var envelope = BuildEnvelope();
        var json = JsonSerializer.Serialize(envelope, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    private Envelope BuildEnvelope()
    {
        var standupInFlight = _coordinator.StandupInFlight;
        var addInFlight = _coordinator.AddInFlight;

        if (!_service.Exists())
            return new Envelope(Status: "missing", FilePath: _service.FilePath, Plan: null, StandupInFlight: standupInFlight, AddInFlight: addInFlight);

        try
        {
            var plan = _service.Load();
            return new Envelope(Status: "ok", FilePath: _service.FilePath, Plan: plan, StandupInFlight: standupInFlight, AddInFlight: addInFlight);
        }
        catch (Exception ex)
        {
            return new Envelope(Status: "error", FilePath: _service.FilePath, Plan: null, StandupInFlight: standupInFlight, AddInFlight: addInFlight, Error: ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachStandupPty();
        DetachTranscriptTailer();
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        _watcher.Reloaded -= OnFileReloaded;
        _watcher.Dispose();
        _speak?.Dispose();
        if (_view.CoreWebView2 is { } core)
            core.WebMessageReceived -= OnWebMessageReceived;
    }

    private sealed record TerminalDataEnvelope(
        string Type,
        string Data);

    private sealed record TerminalSystemNoteEnvelope(
        string Type,
        string Text);

    private sealed record TranscriptEventEnvelope(
        string Type,
        string Kind,
        string Text);

    private sealed record Envelope(
        string Status,
        string FilePath,
        LifePlan? Plan,
        bool StandupInFlight,
        bool AddInFlight,
        string? Error = null);
}

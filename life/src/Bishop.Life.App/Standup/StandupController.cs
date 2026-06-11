using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema.Envelopes;
using Bishop.Life.Core.Web;

namespace Bishop.Life.App.Standup;

/// <summary>
/// Owns the stand-up concern previously inlined in <see cref="LifePlanHost"/>:
/// launches Claude via ConPTY (falling back to <c>wt.exe</c>), forwards keystrokes
/// to the PTY, fans the JSONL transcript out as <c>transcript:event</c> envelopes,
/// dispatches <c>terminal:systemNote</c> bubbles for dropped input and session-end,
/// and runs the post-exit delayed-hide of the stand-up pane.
/// Second slice of the LifePlanHost decomposition (card #1070); first was
/// <see cref="Speak.SpeakController"/> (card #1069).
/// The launcher / tailer / dispatcher seams keep the controller unit-testable
/// without WebView2, Pty.Net, or the filesystem.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class StandupController : IDisposable
{
    private const int DefaultCols = 80;
    private const int DefaultRows = 30;
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(1500);

    public delegate ClaudePtySession? PtyLauncher(string cwd, string claudeArgs, int cols, int rows);
    public delegate void WtLauncher(string cwd, string claudeArgs);
    public delegate IClaudeSessionTailer TailerFactory(string jsonlPath);

    /// <summary>
    /// Production tailer factory — production callers pass this so they don't
    /// need to mention <see cref="ClaudeSessionJsonlTailer"/> directly.
    /// </summary>
    public static readonly TailerFactory DefaultTailerFactory = path => new ClaudeSessionJsonlTailer(path);

    private readonly PtyLauncher _ptyLauncher;
    private readonly WtLauncher _wtLauncher;
    private readonly TailerFactory _tailerFactory;
    private readonly IBrowserChannel _channel;
    private readonly Action<Action> _uiPost;
    private readonly Action<TimeSpan, Action> _scheduleAfter;

    private ClaudePtySession? _pty;
    private PtyInputSequencer? _sequencer;
    private IClaudeSessionTailer? _tailer;
    private int _cols = DefaultCols;
    private int _rows = DefaultRows;
    private bool _disposed;

    /// <summary>Raised synchronously after <see cref="Launch"/> completes.</summary>
    public event Action? Launched;

    /// <summary>Raised on the UI thread when the embedded PTY process exits.</summary>
    public event Action? SessionEnded;

    public StandupController(
        PtyLauncher ptyLauncher,
        WtLauncher wtLauncher,
        TailerFactory tailerFactory,
        IBrowserChannel channel,
        Action<Action> uiPost,
        Action<TimeSpan, Action> scheduleAfter)
    {
        _ptyLauncher = ptyLauncher;
        _wtLauncher = wtLauncher;
        _tailerFactory = tailerFactory;
        _channel = channel;
        _uiPost = uiPost;
        _scheduleAfter = scheduleAfter;
    }

    /// <summary>
    /// Tries ConPTY first; on failure falls through to wt.exe so the user
    /// still gets a working stand-up. <paramref name="sessionId"/> pins the
    /// JSONL transcript path so the tailer doesn't race claude's first write
    /// (card #1059).
    /// </summary>
    public void Launch(string workingDirectory, string claudeArgs, string sessionId)
    {
        if (_disposed) return;

        var pty = _ptyLauncher(workingDirectory, claudeArgs, _cols, _rows);
        if (pty is not null)
        {
            AttachPty(pty);
            AttachTailer(workingDirectory, sessionId);
            PostShow();
        }
        else
        {
            _wtLauncher(workingDirectory, claudeArgs);
        }
        Launched?.Invoke();
    }

    /// <summary>
    /// Forwards a keystroke from the stand-up input into the PTY via
    /// <see cref="PtyInputSequencer"/>, which owns the body-then-Enter split
    /// and inter-write delay from card #1065. Surfaces dropped input via
    /// <c>terminal:systemNote</c> so the next repro names the cause — each
    /// silent failure mode (PTY not attached, Write throws) gets its own
    /// distinct bubble.
    /// </summary>
    public async Task HandleInputAsync(string body, bool submit, CancellationToken ct = default)
    {
        if (_disposed) return;
        var sequencer = _sequencer;
        if (sequencer is null)
        {
            Debug.WriteLine("StandupController: terminal:input dropped — PTY not attached");
            PostSystemNote("[input dropped — PTY not attached]");
            return;
        }
        try
        {
            await sequencer.WriteKeystrokeAsync(body, submit, ct).ConfigureAwait(false);
            Debug.WriteLine($"StandupController: terminal:input wrote body={body.Length} submit={submit}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StandupController: terminal:input Write threw: {ex}");
            PostSystemNote($"[input dropped — Write threw: {ex.Message}]");
        }
    }

    /// <summary>
    /// User-initiated wrap-up (card #1081). Tears down the PTY and tailer,
    /// fires <see cref="SessionEnded"/>, and posts <c>terminal:hide</c>
    /// immediately — no <c>[Claude session ended]</c> note and no
    /// <see cref="HideDelay"/>, both of which are noise when the user is the
    /// one ending the session. No-ops when no PTY is attached so a stray
    /// click after the natural-exit path can't double-hide.
    /// </summary>
    public void End()
    {
        if (_disposed) return;
        if (_pty is null) return;
        DetachPty();
        DetachTailer();
        SessionEnded?.Invoke();
        PostHide();
    }

    /// <summary>
    /// Writes raw bytes straight to the PTY, bypassing
    /// <see cref="PtyInputSequencer"/>. Used by the debug-console overlay
    /// (card #1086, <c>Ctrl+Shift+T</c>) where keystrokes must hit the TUI
    /// without the body/Enter split and inter-write delay that the
    /// stand-up input applies. Drops silently when no PTY is attached.
    /// </summary>
    public void WriteRaw(string data)
    {
        if (_disposed) return;
        if (string.IsNullOrEmpty(data)) return;
        try
        {
            _pty?.Write(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StandupController: terminal:raw-input Write threw: {ex}");
        }
    }

    /// <summary>
    /// Updates the controller's cached viewport and forwards to the PTY if one
    /// is attached. Sizes &lt; 1 (zero-sized layout pass) are ignored so they
    /// don't poison the cached defaults.
    /// </summary>
    public void Resize(int cols, int rows)
    {
        if (cols >= 1) _cols = cols;
        if (rows >= 1) _rows = rows;
        _pty?.Resize(_cols, _rows);
    }

    private void AttachPty(ClaudePtySession pty)
    {
        DetachPty();
        _pty = pty;
        _sequencer = new PtyInputSequencer((data, _) => { pty.Write(data); return Task.CompletedTask; });
        // Card #1086: stream every PTY byte to the viewer as terminal:data from
        // session start. The debug-console xterm consumes the feed into a hidden
        // buffer so toggling Ctrl+Shift+T shows the current TUI screen instantly
        // rather than a torn re-attach. Transcript rendering still comes from the
        // JSONL tailer (card #1059); this channel is for raw screen state only.
        pty.DataReceived += OnPtyData;
        pty.ProcessExited += OnPtyExited;
    }

    private void DetachPty()
    {
        if (_pty is null) return;
        _pty.DataReceived -= OnPtyData;
        _pty.ProcessExited -= OnPtyExited;
        _pty.Dispose();
        _pty = null;
        _sequencer = null;
    }

    private void AttachTailer(string cwd, string sessionId)
    {
        DetachTailer();
        try
        {
            var jsonlPath = ClaudeSessionPaths.ResolveSessionFilePath(cwd, sessionId);
            var tailer = _tailerFactory(jsonlPath);
            tailer.UserMessage += OnTranscriptUser;
            tailer.AssistantText += OnTranscriptAssistant;
            tailer.ToolUse += OnTranscriptTool;
            tailer.ParseFailed += OnTranscriptParseFailed;
            tailer.Start();
            _tailer = tailer;
        }
        catch (Exception ex)
        {
            // Tailer is best-effort: dropping it just means no transcript fan-out.
            Debug.WriteLine($"StandupController: transcript tailer failed: {ex.Message}");
        }
    }

    private void DetachTailer()
    {
        if (_tailer is null) return;
        _tailer.UserMessage -= OnTranscriptUser;
        _tailer.AssistantText -= OnTranscriptAssistant;
        _tailer.ToolUse -= OnTranscriptTool;
        _tailer.ParseFailed -= OnTranscriptParseFailed;
        _tailer.Dispose();
        _tailer = null;
    }

    private void OnPtyData(string data) =>
        _uiPost(() => PostData(data));

    private void OnPtyExited() =>
        _uiPost(() =>
        {
            DetachPty();
            DetachTailer();
            // Card #1065: surface the session-ended cause BEFORE hiding the pane,
            // and delay the hide so the note is visible long enough to read when
            // it races with a user keystroke.
            PostSystemNote("[Claude session ended]");
            SessionEnded?.Invoke();
            _scheduleAfter(HideDelay, PostHide);
        });

    private void OnTranscriptUser(string text) =>
        _uiPost(() => PostTranscript("user", text));

    private void OnTranscriptAssistant(string text) =>
        _uiPost(() => PostTranscript("assistant", text));

    private void OnTranscriptTool(ClaudeSessionJsonlTailer.ToolUseEvent evt) =>
        _uiPost(() => PostTranscript("tool", evt.Summary));

    private void OnTranscriptParseFailed(ClaudeSessionJsonlTailer.ParseFailedEvent evt) =>
        _uiPost(() => PostSystemNote($"Bishop couldn't read Claude session line {evt.LineNumber} — format may have changed"));

    private void PostShow() => _ = _channel.PostAsync(new BareEnvelope(Type: "terminal:show"));
    private void PostHide() => _ = _channel.PostAsync(new BareEnvelope(Type: "terminal:hide"));
    private void PostData(string data) => _ = _channel.PostAsync(new TerminalDataEnvelope(Type: "terminal:data", Data: data));
    private void PostSystemNote(string text) => _ = _channel.PostAsync(new SystemNoteEnvelope(Type: "terminal:systemNote", Text: text));
    private void PostTranscript(string kind, string text) => _ = _channel.PostAsync(new TranscriptEventEnvelope(Type: "transcript:event", Kind: kind, Text: text));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachPty();
        DetachTailer();
    }
}

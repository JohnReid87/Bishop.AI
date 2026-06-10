using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Pty.Net;

namespace Bishop.Life.App;

/// <summary>
/// Owns a single ConPTY-spawned <c>claude</c> process and surfaces its IO as
/// .NET events so <see cref="Standup.StandupController"/> can forward keystrokes
/// into it and react to process exit. Disposing kills the underlying process —
/// there is no separate shutdown handshake.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ClaudePtySession : IDisposable
{
    private readonly IPtyConnection _pty;
    private bool _disposed;

    /// <summary>Raised when the PTY emits stdout/stderr bytes (already decoded to a string by Pty.Net).</summary>
    public event Action<string>? DataReceived;

    /// <summary>Raised exactly once when the underlying process exits.</summary>
    public event Action? ProcessExited;

    public ClaudePtySession(IPtyConnection pty)
    {
        _pty = pty;
        _pty.PtyData += OnPtyData;
        _pty.PtyDisconnected += OnPtyDisconnected;
    }

    public void Write(string data)
    {
        if (_disposed) return;
        _pty.Write(data);
    }

    public void Resize(int cols, int rows)
    {
        if (_disposed) return;
        // Pty.Net uses (width=cols, height=rows). Clamp to ≥1 so a zero-sized
        // pane during layout doesn't blow up the native resize call.
        if (cols < 1) cols = 1;
        if (rows < 1) rows = 1;
        _pty.Resize(cols, rows);
    }

    private void OnPtyData(object sender, string data) => DataReceived?.Invoke(data);

    private void OnPtyDisconnected(object sender) => ProcessExited?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pty.PtyData -= OnPtyData;
        _pty.PtyDisconnected -= OnPtyDisconnected;
        try { _pty.Dispose(); }
        catch (Exception ex)
        {
            // Pty.Net's native handle close can race with the OS reaping the
            // process; the throw is informational, not actionable.
            Debug.WriteLine($"ClaudePtySession: pty dispose threw (ignored): {ex.Message}");
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bishop.Life.App.Standup;

/// <summary>
/// Sequences keystroke writes into a ConPTY-hosted Claude TUI. Encapsulates two
/// rules that previously lived in the JS input handler (card #1065):
/// the body and the Enter (<c>\r</c>) submit must arrive as two distinct
/// readable events, separated by a real wall-clock gap. Back-to-back writes
/// land microseconds apart and ConPTY coalesces them into a single read event,
/// which Claude's raw-mode TUI treats as one keystroke chunk and does not
/// submit until the next event nudges its input loop — the "first message
/// swallowed" repro from #1065. A short <c>Task.Delay</c> forces two events.
///
/// Calls are serialized with a <see cref="SemaphoreSlim"/> so concurrent
/// <see cref="WriteKeystrokeAsync"/> invocations cannot interleave a body
/// with another caller's Enter.
/// </summary>
internal sealed class PtyInputSequencer
{
    /// <summary>Default ConPTY/Claude inter-write gap (card #1065).</summary>
    public static readonly TimeSpan DefaultSubmitDelay = TimeSpan.FromMilliseconds(50);

    private readonly Func<string, CancellationToken, Task> _write;
    private readonly TimeSpan _submitDelay;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PtyInputSequencer(Func<string, CancellationToken, Task> write)
        : this(write, DefaultSubmitDelay)
    {
    }

    public PtyInputSequencer(Func<string, CancellationToken, Task> write, TimeSpan submitDelay)
    {
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _submitDelay = submitDelay;
    }

    /// <summary>
    /// Writes <paramref name="body"/> (if non-empty) and, when
    /// <paramref name="submit"/> is true, a trailing <c>\r</c> after the
    /// configured submit delay. Concurrent calls are serialized.
    /// </summary>
    public async Task WriteKeystrokeAsync(string body, bool submit, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var hasBody = !string.IsNullOrEmpty(body);
            if (hasBody)
            {
                await _write(body, ct).ConfigureAwait(false);
            }
            if (submit)
            {
                if (hasBody && _submitDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_submitDelay, ct).ConfigureAwait(false);
                }
                await _write("\r", ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

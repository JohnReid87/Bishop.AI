using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.App.Standup;
using FluentAssertions;

namespace Bishop.Life.Tests;

/// <summary>
/// Coverage for the body/Enter split + inter-write delay extracted from
/// StandupController in card #1076.
/// </summary>
public class PtyInputSequencerTests
{
    [Fact]
    public async Task WriteKeystroke_BodyOnly_WritesBodyOnce_NoEnter()
    {
        var pty = new FakePty();
        var seq = new PtyInputSequencer(pty.WriteAsync, TimeSpan.Zero);

        await seq.WriteKeystrokeAsync("ls", submit: false, CancellationToken.None);

        pty.Writes.Should().Equal("ls");
    }

    [Fact]
    public async Task WriteKeystroke_SubmitOnly_WritesEnterOnce()
    {
        var pty = new FakePty();
        var seq = new PtyInputSequencer(pty.WriteAsync, TimeSpan.Zero);

        await seq.WriteKeystrokeAsync(string.Empty, submit: true, CancellationToken.None);

        pty.Writes.Should().Equal("\r");
    }

    [Fact]
    public async Task WriteKeystroke_BodyAndSubmit_WritesBodyThenEnter_WithDelay()
    {
        var pty = new FakePty();
        var delay = TimeSpan.FromMilliseconds(40);
        var seq = new PtyInputSequencer(pty.WriteAsync, delay);

        var sw = Stopwatch.StartNew();
        await seq.WriteKeystrokeAsync("ls", submit: true, CancellationToken.None);
        sw.Stop();

        pty.Writes.Should().Equal("ls", "\r");
        // Some slack — Task.Delay can fire a tick early on some schedulers.
        sw.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public async Task WriteKeystroke_ConcurrentCalls_PreserveBodyEnterPairing()
    {
        // Each caller writes its own body then \r. The semaphore must keep
        // each pair contiguous — no other caller's body can land between a
        // body and its trailing \r. Task.Yield in the fake write forces a
        // continuation hop so an unlocked sequencer would interleave.
        var pty = new FakePty { YieldOnWrite = true };
        var seq = new PtyInputSequencer(pty.WriteAsync, TimeSpan.Zero);

        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var body = $"cmd{i}";
            tasks.Add(Task.Run(() => seq.WriteKeystrokeAsync(body, submit: true, CancellationToken.None)));
        }
        await Task.WhenAll(tasks);

        pty.Writes.Should().HaveCount(20);
        for (var i = 0; i < pty.Writes.Count; i += 2)
        {
            pty.Writes[i].Should().StartWith("cmd");
            pty.Writes[i + 1].Should().Be("\r");
        }
    }

    [Fact]
    public async Task WriteKeystroke_WhenWriteThrows_PropagatesAndReleasesLock()
    {
        var pty = new FakePty { NextWriteThrows = new InvalidOperationException("boom") };
        var seq = new PtyInputSequencer(pty.WriteAsync, TimeSpan.Zero);

        var act = async () => await seq.WriteKeystrokeAsync("x", submit: false, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        // Lock released — a second call must succeed.
        await seq.WriteKeystrokeAsync("y", submit: false, CancellationToken.None);
        pty.Writes.Should().Equal("y");
    }

    private sealed class FakePty
    {
        public List<string> Writes { get; } = new();
        public bool YieldOnWrite { get; set; }
        public Exception? NextWriteThrows { get; set; }
        private readonly object _gate = new();

        public async Task WriteAsync(string data, CancellationToken ct)
        {
            if (NextWriteThrows is not null)
            {
                var ex = NextWriteThrows;
                NextWriteThrows = null;
                throw ex;
            }
            if (YieldOnWrite)
            {
                await Task.Yield();
            }
            lock (_gate) Writes.Add(data);
        }
    }
}

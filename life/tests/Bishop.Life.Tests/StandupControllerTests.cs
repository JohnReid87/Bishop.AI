using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.App;
using Bishop.Life.App.Standup;
using Bishop.Life.Core;
using Bishop.Life.Core.Web;
using FluentAssertions;
using Pty.Net;

namespace Bishop.Life.Tests;

/// <summary>
/// Envelope-sequencing coverage for the stand-up slice extracted in card #1070.
/// All tests run synchronously by wiring <c>uiPost</c> to invoke inline and
/// capturing the scheduled hide so the production timer never runs.
/// </summary>
public class StandupControllerTests
{
    [Fact]
    public void Launch_OnPtySuccess_PostsTerminalShow_AndFiresLaunched()
    {
        var harness = new Harness();

        harness.Controller.Launch("cwd", "args", "session-id");

        harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.BareEnvelope>()
            .Which.Type.Should().Be("terminal:show");
        harness.LaunchedCount.Should().Be(1);
    }

    [Fact]
    public void Launch_OnPtyFailure_FallsBackToWt_AndPostsNothing()
    {
        var harness = new Harness(ptyResult: () => null);

        harness.Controller.Launch("C:\\cwd", "/cmd", "sid");

        harness.WtLaunches.Should().ContainSingle()
            .Which.Should().Be(("C:\\cwd", "/cmd"));
        harness.Channel.Posts.Should().BeEmpty();
        harness.LaunchedCount.Should().Be(1);
    }

    [Fact]
    public void HandleInput_WithoutPty_PostsDroppedSystemNote()
    {
        var harness = new Harness();

        harness.Controller.HandleInput("hello");

        harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.SystemNoteEnvelope>()
            .Which.Text.Should().Be("[input dropped — PTY not attached]");
    }

    [Fact]
    public void HandleInput_AfterLaunch_ForwardsToPty()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.Controller.HandleInput("ls\r");

        harness.FakePty.Writes.Should().ContainSingle().Which.Should().Be("ls\r");
        harness.Channel.Posts.Should().BeEmpty();
    }

    [Fact]
    public void HandleInput_WhenPtyWriteThrows_PostsSystemNoteWithReason()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();
        harness.FakePty.NextWriteThrows = new InvalidOperationException("boom");

        harness.Controller.HandleInput("x");

        harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.SystemNoteEnvelope>()
            .Which.Text.Should().Be("[input dropped — Write threw: boom]");
    }

    [Fact]
    public void PtyData_IsForwardedAsTerminalDataEnvelope()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.FakePty.RaisePtyData("hello\r\n");

        var data = harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.DataEnvelope>().Subject;
        data.Type.Should().Be("terminal:data");
        data.Data.Should().Be("hello\r\n");
    }

    [Fact]
    public void TranscriptUserMessage_IsForwardedAsTranscriptEvent()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.LastTailer!.RaiseUser("note one");

        var ev = harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.TranscriptEventEnvelope>().Subject;
        ev.Type.Should().Be("transcript:event");
        ev.Kind.Should().Be("user");
        ev.Text.Should().Be("note one");
    }

    [Fact]
    public void TranscriptAssistantText_IsForwardedAsTranscriptEvent()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.LastTailer!.RaiseAssistant("OK.");

        harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.TranscriptEventEnvelope>()
            .Which.Kind.Should().Be("assistant");
    }

    [Fact]
    public void TranscriptToolUse_IsForwardedAsTranscriptEvent_UsingSummary()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.LastTailer!.RaiseTool(new ClaudeSessionJsonlTailer.ToolUseEvent("Read", "reading foo.cs"));

        var ev = harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.TranscriptEventEnvelope>().Subject;
        ev.Kind.Should().Be("tool");
        ev.Text.Should().Be("reading foo.cs");
    }

    [Fact]
    public void PtyExit_PostsSystemNoteThenSchedulesHide_AndFiresSessionEnded()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Channel.Posts.Clear();

        harness.FakePty.RaisePtyDisconnected();

        harness.Channel.Posts.Should().ContainSingle()
            .Which.Should().BeOfType<StandupController.SystemNoteEnvelope>()
            .Which.Text.Should().Be("[Claude session ended]");
        harness.SessionEndedCount.Should().Be(1);
        harness.PendingSchedules.Should().ContainSingle()
            .Which.delay.Should().Be(TimeSpan.FromMilliseconds(1500));

        // Firing the scheduled action posts terminal:hide.
        harness.PendingSchedules[0].action();
        harness.Channel.Posts.Should().HaveCount(2);
        harness.Channel.Posts[1].Should().BeOfType<StandupController.BareEnvelope>()
            .Which.Type.Should().Be("terminal:hide");
    }

    [Fact]
    public void Resize_ForwardsToPty_AndCachesForNextLaunch()
    {
        var capturedSizes = new List<(int cols, int rows)>();
        var harness = new Harness(captureLaunchSize: (c, r) => capturedSizes.Add((c, r)));

        harness.Controller.Resize(120, 40);
        harness.Controller.Launch("cwd", "args", "sid");

        harness.FakePty.Resizes.Should().BeEmpty(); // resized before launch
        capturedSizes.Should().ContainSingle().Which.Should().Be((120, 40));

        harness.Controller.Resize(100, 30);
        harness.FakePty.Resizes.Should().ContainSingle().Which.Should().Be((100, 30));
    }

    [Fact]
    public void Resize_DegenerateDimensions_FallBackToCachedDefaults()
    {
        // A 0×0 from JS happens during initial layout; the cached defaults
        // (80×30 unless previously resized) are what reach the PTY so the
        // grid doesn't get stuck at zero.
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.FakePty.Resizes.Clear();

        harness.Controller.Resize(0, 0);

        harness.FakePty.Resizes.Should().ContainSingle().Which.Should().Be((80, 30));
    }

    [Fact]
    public void HandleInput_AfterDispose_IsNoop()
    {
        var harness = new Harness();
        harness.Controller.Launch("cwd", "args", "sid");
        harness.Controller.Dispose();
        harness.Channel.Posts.Clear();

        harness.Controller.HandleInput("late");

        harness.Channel.Posts.Should().BeEmpty();
        harness.FakePty.Writes.Should().BeEmpty();
    }

    /// <summary>
    /// Bundles up the test seams. Synchronous <c>uiPost</c> + recorded
    /// <c>scheduleAfter</c> keeps every test deterministic.
    /// </summary>
    private sealed class Harness
    {
        public FakeBrowserChannel Channel { get; } = new();
        public FakePtyConnection FakePty { get; } = new();
        public StandupController Controller { get; }
        public List<(string cwd, string args)> WtLaunches { get; } = new();
        public List<(TimeSpan delay, Action action)> PendingSchedules { get; } = new();
        public FakeTailer? LastTailer { get; private set; }
        public int LaunchedCount { get; private set; }
        public int SessionEndedCount { get; private set; }

        public Harness(Func<ClaudePtySession?>? ptyResult = null, Action<int, int>? captureLaunchSize = null)
        {
            ClaudePtySession? PtyLauncher(string cwd, string args, int cols, int rows)
            {
                captureLaunchSize?.Invoke(cols, rows);
                if (ptyResult is not null) return ptyResult();
                return new ClaudePtySession(FakePty);
            }

            void WtLauncher(string cwd, string args) => WtLaunches.Add((cwd, args));

            IClaudeSessionTailer TailerFactory(string path)
            {
                LastTailer = new FakeTailer();
                return LastTailer;
            }

            Controller = new StandupController(
                ptyLauncher: PtyLauncher,
                wtLauncher: WtLauncher,
                tailerFactory: TailerFactory,
                channel: Channel,
                uiPost: action => action(),
                scheduleAfter: (delay, action) => PendingSchedules.Add((delay, action)));

            Controller.Launched += () => LaunchedCount++;
            Controller.SessionEnded += () => SessionEndedCount++;
        }
    }

    private sealed class FakeBrowserChannel : IBrowserChannel
    {
        public List<object> Posts { get; } = new();

        public Task PostAsync(object envelope, CancellationToken ct = default)
        {
            Posts.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTailer : IClaudeSessionTailer
    {
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }

        public event Action<string>? UserMessage;
        public event Action<string>? AssistantText;
        public event Action<ClaudeSessionJsonlTailer.ToolUseEvent>? ToolUse;

        public void Start() => Started = true;
        public void Dispose() => Disposed = true;

        public void RaiseUser(string text) => UserMessage?.Invoke(text);
        public void RaiseAssistant(string text) => AssistantText?.Invoke(text);
        public void RaiseTool(ClaudeSessionJsonlTailer.ToolUseEvent evt) => ToolUse?.Invoke(evt);
    }

    private sealed class FakePtyConnection : IPtyConnection
    {
        public List<string> Writes { get; } = new();
        public List<(int cols, int rows)> Resizes { get; } = new();
        public bool Disposed { get; private set; }
        public Exception? NextWriteThrows { get; set; }

        public event PtyDataEventArgs? PtyData;
        public event PtyDisconnectedEventArgs? PtyDisconnected;

        public void Write(string data)
        {
            if (NextWriteThrows is not null)
            {
                var ex = NextWriteThrows;
                NextWriteThrows = null;
                throw ex;
            }
            Writes.Add(data);
        }
        public void Write(char data) => Write(data.ToString());
        public Task WriteAsync(string data) { Write(data); return Task.CompletedTask; }
        public Task WriteAsync(char data) { Write(data.ToString()); return Task.CompletedTask; }
        public void Resize(int width, int height) => Resizes.Add((width, height));

        public void RaisePtyData(string data) => PtyData?.Invoke(this, data);
        public void RaisePtyDisconnected() => PtyDisconnected?.Invoke(this);

        public void Dispose() => Disposed = true;
    }
}

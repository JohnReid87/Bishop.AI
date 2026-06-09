using Bishop.Life.App;
using FluentAssertions;
using Pty.Net;

namespace Bishop.Life.Tests;

public class ClaudePtySessionTests
{
    [Fact]
    public void DataReceived_RaisesWhenUnderlyingPtyEmitsData()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);
        string? captured = null;
        session.DataReceived += s => captured = s;

        fake.RaisePtyData("hello\r\n");

        captured.Should().Be("hello\r\n");
    }

    [Fact]
    public void ProcessExited_RaisesWhenUnderlyingPtyDisconnects()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);
        var exited = false;
        session.ProcessExited += () => exited = true;

        fake.RaisePtyDisconnected();

        exited.Should().BeTrue();
    }

    [Fact]
    public void Write_ForwardsToUnderlyingPty()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);

        session.Write("ls\r");

        fake.Writes.Should().ContainSingle().Which.Should().Be("ls\r");
    }

    [Fact]
    public void Resize_ClampsDegenerateDimensionsToOne()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);

        // 0×0 happens during layout when the container isn't measurable yet.
        // Forwarding it to native ConPTY blows up; clamp so the call survives.
        session.Resize(0, 0);

        fake.Resizes.Should().ContainSingle().Which.Should().Be((1, 1));
    }

    [Fact]
    public void Resize_ForwardsValidDimensionsUnchanged()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);

        session.Resize(120, 30);

        fake.Resizes.Should().ContainSingle().Which.Should().Be((120, 30));
    }

    [Fact]
    public void Dispose_UnsubscribesFromUnderlyingEvents()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);
        var dataCalls = 0;
        var exitCalls = 0;
        session.DataReceived += _ => dataCalls++;
        session.ProcessExited += () => exitCalls++;

        session.Dispose();
        fake.RaisePtyData("after-dispose");
        fake.RaisePtyDisconnected();

        dataCalls.Should().Be(0);
        exitCalls.Should().Be(0);
        fake.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Write_AfterDispose_IsNoop()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);

        session.Dispose();
        session.Write("late");

        fake.Writes.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var fake = new FakePtyConnection();
        var session = new ClaudePtySession(fake);

        session.Dispose();
        var act = () => session.Dispose();

        act.Should().NotThrow();
    }

    private sealed class FakePtyConnection : IPtyConnection
    {
        public List<string> Writes { get; } = new();
        public List<(int cols, int rows)> Resizes { get; } = new();
        public bool Disposed { get; private set; }

        public event PtyDataEventArgs? PtyData;
        public event PtyDisconnectedEventArgs? PtyDisconnected;

        public void Write(string data) => Writes.Add(data);
        public void Write(char data) => Writes.Add(data.ToString());
        public Task WriteAsync(string data) { Writes.Add(data); return Task.CompletedTask; }
        public Task WriteAsync(char data) { Writes.Add(data.ToString()); return Task.CompletedTask; }
        public void Resize(int width, int height) => Resizes.Add((width, height));

        public void RaisePtyData(string data) => PtyData?.Invoke(this, data);
        public void RaisePtyDisconnected() => PtyDisconnected?.Invoke(this);

        public void Dispose() => Disposed = true;
    }
}

using Bishop.App.WorkNext;
using FluentAssertions;

namespace Bishop.Tests.App.WorkNext;

public sealed class WorkNextHeartbeatTests : IDisposable
{
    private readonly string _bishopDir;
    private readonly string _runningFile;
    private readonly string _stopFile;

    public WorkNextHeartbeatTests()
    {
        _bishopDir = Path.Combine(Path.GetTempPath(), "bishop-heartbeat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_bishopDir);
        _runningFile = Path.Combine(_bishopDir, WorkNextHeartbeat.RunningFileName);
        _stopFile = Path.Combine(_bishopDir, WorkNextHeartbeat.StopFileName);
    }

    public void Dispose()
    {
        try { Directory.Delete(_bishopDir, recursive: true); } catch { }
    }

    [Fact]
    public void NoHeartbeatFile_ReturnsIdle()
    {
        var state = WorkNextHeartbeat.ReadState(_bishopDir, _ => true);

        state.IsRunning.Should().BeFalse();
        state.IsStopping.Should().BeFalse();
    }

    [Fact]
    public void HeartbeatWithLivePid_ReturnsRunning_AndPreservesFile()
    {
        File.WriteAllText(_runningFile, $"1234{Environment.NewLine}2026-05-22T00:00:00Z{Environment.NewLine}");

        var state = WorkNextHeartbeat.ReadState(_bishopDir, pid => pid == 1234);

        state.IsRunning.Should().BeTrue();
        state.IsStopping.Should().BeFalse();
        File.Exists(_runningFile).Should().BeTrue();
    }

    [Fact]
    public void HeartbeatWithLivePidAndStopFile_ReportsStopping()
    {
        File.WriteAllText(_runningFile, $"1234{Environment.NewLine}2026-05-22T00:00:00Z{Environment.NewLine}");
        File.WriteAllText(_stopFile, "");

        var state = WorkNextHeartbeat.ReadState(_bishopDir, _ => true);

        state.IsRunning.Should().BeTrue();
        state.IsStopping.Should().BeTrue();
    }

    [Fact]
    public void HeartbeatWithDeadPid_DeletesFile_AndReportsIdle()
    {
        File.WriteAllText(_runningFile, $"9999{Environment.NewLine}2026-05-22T00:00:00Z{Environment.NewLine}");

        var state = WorkNextHeartbeat.ReadState(_bishopDir, _ => false);

        state.IsRunning.Should().BeFalse();
        state.IsStopping.Should().BeFalse();
        File.Exists(_runningFile).Should().BeFalse();
    }

    [Fact]
    public void HeartbeatWithUnparseableContent_DeletesFile_AndReportsIdle()
    {
        File.WriteAllText(_runningFile, "not-a-pid");

        var state = WorkNextHeartbeat.ReadState(_bishopDir, _ => true);

        state.IsRunning.Should().BeFalse();
        state.IsStopping.Should().BeFalse();
        File.Exists(_runningFile).Should().BeFalse();
    }

    [Fact]
    public void HeartbeatWithDeadPid_AndStopFile_ClearsRunningAndIgnoresStopFile()
    {
        File.WriteAllText(_runningFile, $"9999{Environment.NewLine}");
        File.WriteAllText(_stopFile, "");

        var state = WorkNextHeartbeat.ReadState(_bishopDir, _ => false);

        state.IsRunning.Should().BeFalse();
        state.IsStopping.Should().BeFalse();
        File.Exists(_runningFile).Should().BeFalse();
    }

    [Fact]
    public void ReadState_UsesEnvironmentProcessIdAsLiveProcess_ByDefault()
    {
        File.WriteAllText(_runningFile, $"{Environment.ProcessId}{Environment.NewLine}");

        var state = WorkNextHeartbeat.ReadState(_bishopDir);

        state.IsRunning.Should().BeTrue();
        File.Exists(_runningFile).Should().BeTrue();
    }

    [Fact]
    public void IsProcessAlive_CurrentProcess_ReturnsTrue()
    {
        WorkNextHeartbeat.IsProcessAlive(Environment.ProcessId).Should().BeTrue();
    }

    [Fact]
    public void IsProcessAlive_ZeroPid_ReturnsFalse()
    {
        WorkNextHeartbeat.IsProcessAlive(0).Should().BeFalse();
    }

    [Fact]
    public void IsProcessAlive_NegativePid_ReturnsFalse()
    {
        WorkNextHeartbeat.IsProcessAlive(-1).Should().BeFalse();
    }
}

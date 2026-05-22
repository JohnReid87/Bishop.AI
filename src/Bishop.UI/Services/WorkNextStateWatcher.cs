using Bishop.App.WorkNext;

namespace Bishop.UI.Services;

internal sealed class WorkNextStateWatcher : IDisposable
{
    private readonly string _bishopDir;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer? _periodicCheck;
    private CancellationTokenSource? _debounceCts;

    public event EventHandler<WorkNextState>? StateChanged;

    public WorkNextState CurrentState { get; private set; } = new(false, false);

    public WorkNextStateWatcher(string workspacePath)
    {
        _bishopDir = Path.Combine(workspacePath, WorkNextHeartbeat.DirectoryName);

        try
        {
            Directory.CreateDirectory(_bishopDir);
            _watcher = new FileSystemWatcher(_bishopDir)
            {
                Filter = "worknext.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;

            // FileSystemWatcher does not fire when the heartbeat's PID dies without
            // deleting the file. A periodic re-check catches that case.
            _periodicCheck = new Timer(_ => Reevaluate(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Workspace directory missing or inaccessible — degrade to idle.
        }

        Reevaluate();
    }

    public void RequestStop()
    {
        try
        {
            Directory.CreateDirectory(_bishopDir);
            var stopFile = Path.Combine(_bishopDir, WorkNextHeartbeat.StopFileName);
            File.WriteAllText(stopFile, string.Empty);
        }
        catch
        {
            // Best-effort.
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        _ = FireAfterDebounceAsync(_debounceCts.Token);
    }

    private async Task FireAfterDebounceAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
            Reevaluate();
        }
        catch (OperationCanceledException) { }
    }

    private void Reevaluate()
    {
        var newState = WorkNextHeartbeat.ReadState(_bishopDir);
        if (newState != CurrentState)
        {
            CurrentState = newState;
            StateChanged?.Invoke(this, newState);
        }
    }

    public void Dispose()
    {
        _periodicCheck?.Dispose();
        _debounceCts?.Dispose();
        _watcher?.Dispose();
    }
}

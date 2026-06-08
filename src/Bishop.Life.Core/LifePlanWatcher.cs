namespace Bishop.Life.Core;

/// <summary>
/// FileSystemWatcher wrapper that debounces filesystem events (a single save through
/// .tmp + rename produces several raw events) and raises a single <see cref="Reloaded"/>
/// event per logical change.
/// </summary>
public sealed class LifePlanWatcher : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    private readonly string _filePath;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _timer;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private bool _pending;
    private bool _disposed;

    public event EventHandler? Reloaded;

    public LifePlanWatcher(string filePath) : this(filePath, DefaultDebounce) { }

    public LifePlanWatcher(string filePath, TimeSpan debounce)
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
        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Renamed += OnFsEvent;

        _timer = new System.Threading.Timer(Fire, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    private void OnFsEvent(object sender, FileSystemEventArgs e)
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
        if (shouldFire)
            Reloaded?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFsEvent;
        _watcher.Created -= OnFsEvent;
        _watcher.Renamed -= OnFsEvent;
        _watcher.Dispose();
        _timer.Dispose();
    }
}

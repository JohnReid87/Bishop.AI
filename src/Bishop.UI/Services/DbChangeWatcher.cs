namespace Bishop.UI.Services;

internal sealed class DbChangeWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private CancellationTokenSource? _debounceCts;

    public event EventHandler? DatabaseChanged;

    public DbChangeWatcher(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath)!;
        var fileName = Path.GetFileName(dbPath);

        _watcher = new FileSystemWatcher(dir)
        {
            Filter = $"{fileName}*",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
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
            DatabaseChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _debounceCts?.Dispose();
        _watcher.Dispose();
    }
}

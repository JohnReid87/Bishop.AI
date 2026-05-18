using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace Bishop.UI.ViewModels;

public sealed partial class WorkspaceNotesViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private string _workspacePath = string.Empty;
    private string _lastSavedContent = string.Empty;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private bool _isLoadingFromFile;

    [ObservableProperty]
    public partial string NotesContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsExternalChangeBarVisible { get; set; }

    [ObservableProperty]
    public partial double PanelHeight { get; set; } = 200;

    public string ChevronGlyph => IsExpanded ? "" : "";

    public WorkspaceNotesViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ChevronGlyph));

    partial void OnNotesContentChanged(string value)
    {
        if (_isLoadingFromFile) return;
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        _ = DebounceAndSaveAsync(_debounceCts.Token);
    }

    private async Task DebounceAndSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(500, ct);
            await WriteNotesAsync(NotesContent);
        }
        catch (OperationCanceledException) { }
    }

    public async Task LoadAsync(string workspacePath)
    {
        _debounceCts?.Cancel();
        if (!string.IsNullOrEmpty(_workspacePath))
            await WriteNotesAsync(NotesContent);

        _watcher?.Dispose();
        _watcher = null;

        _workspacePath = workspacePath;
        IsExternalChangeBarVisible = false;

        var content = await ReadNotesAsync();
        _lastSavedContent = content;
        _isLoadingFromFile = true;
        NotesContent = content;
        _isLoadingFromFile = false;

        if (!Directory.Exists(workspacePath)) return;

        _watcher = new FileSystemWatcher(workspacePath, "BISHOP_NOTES.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var fileContent = await ReadNotesAsync();
                if (fileContent == _lastSavedContent) return;

                var isDirty = NotesContent != _lastSavedContent;
                if (!isDirty)
                {
                    _lastSavedContent = fileContent;
                    _isLoadingFromFile = true;
                    NotesContent = fileContent;
                    _isLoadingFromFile = false;
                }
                else
                {
                    IsExternalChangeBarVisible = true;
                }
            }
            catch { }
        });
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsExpanded)
        {
            _debounceCts?.Cancel();
            await WriteNotesAsync(NotesContent);
        }
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        _debounceCts?.Cancel();
        var content = await ReadNotesAsync();
        _lastSavedContent = content;
        _isLoadingFromFile = true;
        NotesContent = content;
        _isLoadingFromFile = false;
        IsExternalChangeBarVisible = false;
    }

    [RelayCommand]
    private void KeepEdits()
    {
        IsExternalChangeBarVisible = false;
    }

    public async Task FlushAsync()
    {
        _debounceCts?.Cancel();
        await WriteNotesAsync(NotesContent);
    }

    private async Task<string> ReadNotesAsync()
    {
        var path = NotesFilePath;
        if (!File.Exists(path)) return string.Empty;
        try { return await File.ReadAllTextAsync(path); }
        catch { return string.Empty; }
    }

    private async Task WriteNotesAsync(string content)
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        if (_watcher is not null) _watcher.EnableRaisingEvents = false;
        try
        {
            await File.WriteAllTextAsync(NotesFilePath, content);
            _lastSavedContent = content;
        }
        finally
        {
            if (_watcher is not null) _watcher.EnableRaisingEvents = true;
        }
    }

    private string NotesFilePath => Path.Combine(_workspacePath, "BISHOP_NOTES.md");

    public void Dispose()
    {
        _debounceCts?.Dispose();
        _watcher?.Dispose();
    }
}

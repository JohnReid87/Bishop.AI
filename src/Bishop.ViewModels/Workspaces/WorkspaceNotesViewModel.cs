using Bishop.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Text.Json;

namespace Bishop.ViewModels.Workspaces;

// Lifetime: DI-registered Transient. Holds a FileSystemWatcher + CancellationTokenSource,
// so the host view must call Dispose() when the VM is no longer needed — WinUI 3 does not
// dispose ViewModels automatically. Current owner: WorkspaceDetailPage.OnNavigatedFrom.
public sealed partial class WorkspaceNotesViewModel : ObservableObject, IDisposable
{
    private readonly IUiDispatcher _uiDispatcher;
    private readonly TimeProvider _timeProvider;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;
    private string _lastSavedContent = string.Empty;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private bool _isLoadingFromFile;
    private bool _isLoadingPrefs;

    [ObservableProperty]
    public partial string NotesContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsExternalChangeBarVisible { get; set; }

    [ObservableProperty]
    public partial double PanelHeight { get; set; } = 200;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveStatusOpacity))]
    public partial bool SaveStatusIsEditing { get; set; }

    [ObservableProperty]
    public partial bool SaveStatusIsError { get; set; }

    [ObservableProperty]
    public partial string SaveStatusText { get; set; } = string.Empty;

    public double SaveStatusOpacity => SaveStatusIsEditing ? 0.5 : 1.0;

    public string ChevronGlyph => IsExpanded ? "" : "";

    public WorkspaceNotesViewModel(IUiDispatcher uiDispatcher, TimeProvider timeProvider)
    {
        _uiDispatcher = uiDispatcher;
        _timeProvider = timeProvider;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChevronGlyph));
        if (!_isLoadingPrefs && _workspaceId != Guid.Empty)
            _ = SafeAsync.RunAsync(SavePrefsAsync);
    }

    partial void OnNotesContentChanged(string value)
    {
        if (_isLoadingFromFile) return;
        SaveStatusIsEditing = true;
        SaveStatusIsError = false;
        SaveStatusText = "Editing…";
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _ = SafeAsync.RunAsync(() => DebounceAndSaveAsync(token));
    }

    private async Task DebounceAndSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(500, ct);
            await WriteNotesAsync(NotesContent);
        }
        catch (OperationCanceledException) { } // debounce superseded by a newer edit — drop this stale save
    }

    public async Task LoadAsync(Guid workspaceId, string workspacePath)
    {
        _debounceCts?.Cancel();
        if (!string.IsNullOrEmpty(_workspacePath))
            await WriteNotesAsync(NotesContent);

        TearDownWatcher();

        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        IsExternalChangeBarVisible = false;

        var content = await ReadNotesAsync();
        if (!File.Exists(NotesFilePath) && Directory.Exists(workspacePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NotesFilePath)!);
            await File.WriteAllTextAsync(NotesFilePath, string.Empty);
        }
        _lastSavedContent = content;
        SaveStatusIsEditing = false;
        SaveStatusIsError = false;
        SaveStatusText = File.Exists(NotesFilePath)
            ? $"Saved {File.GetLastWriteTime(NotesFilePath):HH:mm:ss}"
            : string.Empty;
        _isLoadingFromFile = true;
        NotesContent = content;
        _isLoadingFromFile = false;

        await LoadPrefsAsync();

        if (!Directory.Exists(workspacePath)) return;

        var notesDir = Path.Combine(workspacePath, ".bishop");
        Directory.CreateDirectory(notesDir);
        _watcher = new FileSystemWatcher(notesDir, "BISHOP_NOTES.md")
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
        _uiDispatcher.TryEnqueue(async () =>
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
        await SavePrefsAsync();
    }

    public async Task QuickSaveAsync()
    {
        _debounceCts?.Cancel();
        await WriteNotesAsync(NotesContent);
        if (!SaveStatusIsError)
            IsExternalChangeBarVisible = false;
    }

    private async Task LoadPrefsAsync()
    {
        if (!File.Exists(PrefsFilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(PrefsFilePath);
            var all = JsonSerializer.Deserialize<Dictionary<string, WorkspaceNotesPrefs>>(json);
            if (all is null || !all.TryGetValue(_workspaceId.ToString(), out var prefs)) return;

            _isLoadingPrefs = true;
            IsExpanded = prefs.IsExpanded;
            PanelHeight = prefs.PanelHeight > 0 ? prefs.PanelHeight : 200;
            _isLoadingPrefs = false;
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] LoadPrefsAsync: {ex.Message}"); }
    }

    private async Task SavePrefsAsync()
    {
        if (_workspaceId == Guid.Empty) return;
        try
        {
            Dictionary<string, WorkspaceNotesPrefs> all = [];
            if (File.Exists(PrefsFilePath))
            {
                var existing = await File.ReadAllTextAsync(PrefsFilePath);
                all = JsonSerializer.Deserialize<Dictionary<string, WorkspaceNotesPrefs>>(existing) ?? [];
            }
            all[_workspaceId.ToString()] = new WorkspaceNotesPrefs(IsExpanded, PanelHeight);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefsFilePath)!);
            await File.WriteAllTextAsync(PrefsFilePath, JsonSerializer.Serialize(all));
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SavePrefsAsync: {ex.Message}"); }
    }

    private async Task<string> ReadNotesAsync()
    {
        var path = NotesFilePath;
        if (!File.Exists(path)) return string.Empty;
        try { return await File.ReadAllTextAsync(path); }
        catch { return string.Empty; } // intentional: notes file unreadable returns empty
    }

    private async Task WriteNotesAsync(string content)
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        if (content == _lastSavedContent)
        {
            if (!SaveStatusIsError)
                SaveStatusText = $"Saved {_timeProvider.GetLocalNow().DateTime:HH:mm:ss}";
            return;
        }
        if (_watcher is not null) _watcher.EnableRaisingEvents = false;
        try
        {
            await File.WriteAllTextAsync(NotesFilePath, content);
            _lastSavedContent = content;
            SaveStatusText = $"Saved {_timeProvider.GetLocalNow().DateTime:HH:mm:ss}";
            SaveStatusIsEditing = false;
            SaveStatusIsError = false;
        }
        catch (Exception ex)
        {
            SaveStatusText = $"Save failed — {ex.Message}";
            SaveStatusIsError = true;
        }
        finally
        {
            if (_watcher is not null) _watcher.EnableRaisingEvents = true;
        }
    }

    private string NotesFilePath => Path.Combine(_workspacePath, ".bishop", "BISHOP_NOTES.md");

    private static string PrefsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bishop.AI", "notes-prefs.json");

    private void TearDownWatcher()
    {
        if (_watcher is null) return;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Deleted -= OnFileChanged;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        TearDownWatcher();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    private sealed record WorkspaceNotesPrefs(bool IsExpanded, double PanelHeight);
}

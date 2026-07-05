using Bishop.App.Batches.ReconcileOrphanedBatches;
using Bishop.App.Services;
using Bishop.App.Services.CatMode;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.App.Workspaces.RemoveWorkspace;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.SetWorkspaceHidden;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Workspaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace Bishop.ViewModels.Shared;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly WorkspaceChangeNotifier _notifier;
    private readonly IUiDispatcher _dispatcher;
    private readonly IErrorBus _errorBus;
    private readonly ISafeAsyncRunner _safeAsync;

    public ICatModeService CatMode { get; }

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public ObservableCollection<ErrorNotificationViewModel> Notifications => _errorBus.Notifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceListEmpty))]
    [NotifyPropertyChangedFor(nameof(IsContentEmpty))]
    public partial WorkspaceItemViewModel? SelectedWorkspace { get; set; }

    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentEmpty))]
    public partial bool IsWorkspacelessPageActive { get; set; }

    public bool IsWorkspaceListEmpty => Workspaces.Count == 0;

    public bool IsContentEmpty => SelectedWorkspace is null && !IsWorkspacelessPageActive;

    public bool IsCatModeActive => CatMode.IsActive;

    private readonly string _navPrefsFilePath;

    public MainWindowViewModel(ISender mediator, ICatModeService catMode, WorkspaceChangeNotifier notifier, IUiDispatcher dispatcher, IErrorBus errorBus, ISafeAsyncRunner safeAsync)
        : this(mediator, catMode, notifier, dispatcher, errorBus, safeAsync, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "nav-prefs.json"))
    { }

    internal MainWindowViewModel(ISender mediator, ICatModeService catMode, WorkspaceChangeNotifier notifier, IUiDispatcher dispatcher, IErrorBus errorBus, ISafeAsyncRunner safeAsync, string navPrefsFilePath)
    {
        _mediator = mediator;
        _notifier = notifier;
        _dispatcher = dispatcher;
        _errorBus = errorBus;
        _safeAsync = safeAsync;
        CatMode = catMode;
        _navPrefsFilePath = navPrefsFilePath;
        Workspaces.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsWorkspaceListEmpty));
        CatMode.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ICatModeService.IsActive))
                OnPropertyChanged(nameof(IsCatModeActive));
        };
        _notifier.WorkspacesChanged += OnWorkspacesChanged;
    }

    private void OnWorkspacesChanged() =>
        _dispatcher.TryEnqueue(ReloadPreservingSelectionAsync);

    private async Task ReloadPreservingSelectionAsync()
    {
        var currentId = SelectedWorkspace?.Id;
        await ReloadWorkspacesListAsync();
        if (currentId is null)
            return;
        SelectedWorkspace = Workspaces.FirstOrDefault(w => w.Id == currentId)
            ?? Workspaces.FirstOrDefault();
    }

    [RelayCommand]
    private void ToggleCatMode() => CatMode.Toggle();

    public async Task LoadAsync()
    {
        await _mediator.Send(new ReconcileOrphanedBatchesCommand());
        var prefs = await LoadNavPrefsAsync();
        await ReloadWorkspacesListAsync();
        if (prefs?.LastSelectedWorkspaceId is { } id)
            SelectedWorkspace = Workspaces.FirstOrDefault(w => w.Id == id);
    }

    partial void OnShowHiddenChanged(bool value) =>
        _dispatcher.TryEnqueue(ReloadPreservingSelectionAsync);

    private async Task ReloadWorkspacesListAsync()
    {
        var workspaces = await _mediator.Send(new ListWorkspacesQuery(IncludeHidden: ShowHidden));
        Workspaces.Clear();
        foreach (var w in workspaces)
            Workspaces.Add(ToViewModel(w));
        var items = Workspaces.ToList();
        await Task.WhenAll(items.Select(async item =>
        {
            var missing = !await Task.Run(() => Directory.Exists(item.Path));
            item.IsPathMissing = missing;
        }));
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        SyncSelectedWorkspaceSelection(value);
        if (value is not null)
            IsWorkspacelessPageActive = false;
        _ = _safeAsync.RunAsync(SaveNavPrefsAsync);
    }

    private void SyncSelectedWorkspaceSelection(WorkspaceItemViewModel? selected)
    {
        foreach (var w in Workspaces)
            w.IsSelected = w == selected;
    }

    [RelayCommand]
    public async Task AddWorkspaceAsync(AddWorkspaceDialogViewModel dialogVm)
    {
        Bishop.Core.Workspace workspace;
        if (dialogVm.IsPickExisting)
        {
            var result = await _mediator.Send(
                new InitWorkspaceCommand(dialogVm.FolderPath, dialogVm.Name,
                    ArchivedAction: InitWorkspaceArchivedAction.Restore));
            workspace = result.Workspace;
        }
        else
        {
            workspace = await _mediator.Send(
                new CreateWorkspaceCommand(dialogVm.Name, dialogVm.EffectivePath, InitGit: true));
        }
        var vm = ToViewModel(workspace);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
        _notifier.NotifyChanged();
    }

    [RelayCommand]
    public async Task DeleteWorkspaceAsync(WorkspaceItemViewModel item)
    {
        await _mediator.Send(new RemoveWorkspaceCommand(item.Id));
        await _mediator.Send(new PurgeWorkspaceCommand(item.Id));
        Workspaces.Remove(item);
        if (SelectedWorkspace == item)
            SelectedWorkspace = null;
    }

    [RelayCommand]
    public async Task RenameWorkspaceAsync(WorkspaceItemViewModel item)
    {
        await _mediator.Send(new UpdateWorkspaceCommand(item.Id, item.Name, item.Path));
    }

    public async Task RepathWorkspaceAsync(WorkspaceItemViewModel item, string newPath)
    {
        var workspace = await _mediator.Send(new UpdateWorkspaceCommand(item.Id, item.Name, newPath));
        item.Path = workspace.Path;
    }

    [RelayCommand]
    public async Task HideWorkspaceAsync(WorkspaceItemViewModel item)
    {
        await _mediator.Send(new SetWorkspaceHiddenCommand(item.Id, true));
        item.IsHidden = true;
        await ReloadPreservingSelectionAsync();
    }

    [RelayCommand]
    public async Task UnhideWorkspaceAsync(WorkspaceItemViewModel item)
    {
        await _mediator.Send(new SetWorkspaceHiddenCommand(item.Id, false));
        item.IsHidden = false;
        await ReloadPreservingSelectionAsync();
    }

    public async Task PersistReorderAsync(IEnumerable<WorkspaceItemViewModel> orderedItems)
    {
        var ids = orderedItems.Select(w => w.Id).ToList();
        await _mediator.Send(new ReorderWorkspacesCommand(ids));

        var position = 1;
        foreach (var item in orderedItems)
            item.Position = position++;
    }

    private static WorkspaceItemViewModel ToViewModel(Bishop.Core.Workspace w) =>
        new() { Id = w.Id, Name = w.Name, Path = w.Path, Position = w.Position, IsHidden = w.IsHidden };

    private async Task<NavPrefs?> LoadNavPrefsAsync()
    {
        if (!File.Exists(NavPrefsFilePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(NavPrefsFilePath);
            return JsonSerializer.Deserialize<NavPrefs>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Bishop] LoadNavPrefsAsync: {ex.Message}");
            return null;
        }
    }

    private async Task SaveNavPrefsAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NavPrefsFilePath)!);
            await File.WriteAllTextAsync(NavPrefsFilePath, JsonSerializer.Serialize(new NavPrefs(SelectedWorkspace?.Id)));
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SaveNavPrefsAsync: {ex.Message}"); }
    }

    private string NavPrefsFilePath => _navPrefsFilePath;

    private sealed record NavPrefs(Guid? LastSelectedWorkspaceId = null);
}

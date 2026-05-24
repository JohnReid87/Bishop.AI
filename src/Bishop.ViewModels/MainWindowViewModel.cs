using Bishop.App.Services;
using Bishop.App.Services.CatMode;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.UpdateWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace Bishop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IWorkspaceChangeNotifier _notifier;
    private readonly IUiDispatcher _dispatcher;

    public ICatModeService CatMode { get; }

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceListEmpty))]
    [NotifyPropertyChangedFor(nameof(IsContentEmpty))]
    public partial WorkspaceItemViewModel? SelectedWorkspace { get; set; }

    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; } = true;

    public bool IsWorkspaceListEmpty => Workspaces.Count == 0;

    public bool IsContentEmpty => SelectedWorkspace is null;

    public bool IsCatModeActive => CatMode.IsActive;

    private readonly string _navPrefsFilePath;

    public MainWindowViewModel(ISender mediator, ICatModeService catMode, IWorkspaceChangeNotifier notifier, IUiDispatcher dispatcher)
        : this(mediator, catMode, notifier, dispatcher, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "nav-prefs.json"))
    { }

    internal MainWindowViewModel(ISender mediator, ICatModeService catMode, IWorkspaceChangeNotifier notifier, IUiDispatcher dispatcher, string navPrefsFilePath)
    {
        _mediator = mediator;
        _notifier = notifier;
        _dispatcher = dispatcher;
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

    private void OnWorkspacesChanged()
    {
        _dispatcher.TryEnqueue(async () =>
        {
            var currentId = SelectedWorkspace?.Id;
            await ReloadWorkspacesListAsync();
            if (currentId is not null && !Workspaces.Any(w => w.Id == currentId))
                SelectedWorkspace = Workspaces.FirstOrDefault();
        });
    }

    [RelayCommand]
    private void ToggleCatMode() => CatMode.Toggle();

    public async Task LoadAsync()
    {
        var prefs = await LoadNavPrefsAsync();
        await ReloadWorkspacesListAsync();
        if (prefs?.LastSelectedWorkspaceId is { } id)
            SelectedWorkspace = Workspaces.FirstOrDefault(w => w.Id == id);
    }

    private async Task ReloadWorkspacesListAsync()
    {
        var workspaces = await _mediator.Send(new ListWorkspacesQuery());
        Workspaces.Clear();
        foreach (var w in workspaces)
            Workspaces.Add(ToViewModel(w));
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        foreach (var w in Workspaces)
            w.IsSelected = w == value;
        if (value is not null)
            value.IsPathMissing = !Directory.Exists(value.Path);
        _ = SaveNavPrefsAsync();
    }

    partial void OnIsPaneOpenChanged(bool value) => _ = SaveNavPrefsAsync();

    [RelayCommand]
    public async Task AddWorkspaceAsync(AddWorkspaceDialogViewModel dialogVm)
    {
        var result = await _mediator.Send(
            new InitWorkspaceCommand(dialogVm.FolderPath, dialogVm.Name,
                ArchivedAction: InitWorkspaceArchivedAction.Restore));
        var vm = ToViewModel(result.Workspace);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
        _notifier.NotifyChanged();
    }

    [RelayCommand]
    public async Task DeleteWorkspaceAsync(WorkspaceItemViewModel item)
    {
        await _mediator.Send(new DeleteWorkspaceCommand(item.Id));
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

    public async Task PersistReorderAsync(IEnumerable<WorkspaceItemViewModel> orderedItems)
    {
        var ids = orderedItems.Select(w => w.Id).ToList();
        await _mediator.Send(new ReorderWorkspacesCommand(ids));

        var position = 1;
        foreach (var item in orderedItems)
            item.Position = position++;
    }

    private static WorkspaceItemViewModel ToViewModel(Bishop.Core.Workspace w) =>
        new() { Id = w.Id, Name = w.Name, Path = w.Path, Position = w.Position, GitHubRepo = w.GitHubRepo };

    private async Task<NavPrefs?> LoadNavPrefsAsync()
    {
        if (!File.Exists(NavPrefsFilePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(NavPrefsFilePath);
            var prefs = JsonSerializer.Deserialize<NavPrefs>(json);
            if (prefs is not null)
                IsPaneOpen = prefs.IsPaneOpen;
            return prefs;
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
            await File.WriteAllTextAsync(NavPrefsFilePath, JsonSerializer.Serialize(new NavPrefs(IsPaneOpen, SelectedWorkspace?.Id)));
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SaveNavPrefsAsync: {ex.Message}"); }
    }

    private string NavPrefsFilePath => _navPrefsFilePath;

    private sealed record NavPrefs(bool IsPaneOpen, Guid? LastSelectedWorkspaceId = null);
}

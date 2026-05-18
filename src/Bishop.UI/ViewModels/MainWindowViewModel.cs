using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.UpdateWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.IO;

namespace Bishop.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    public partial WorkspaceItemViewModel? SelectedWorkspace { get; set; }

    public Visibility EmptyStateVisibility =>
        Workspaces.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public MainWindowViewModel(IMediator mediator)
    {
        _mediator = mediator;
        Workspaces.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    public async Task LoadAsync()
    {
        var workspaces = await _mediator.Send(new ListWorkspacesQuery());
        Workspaces.Clear();
        foreach (var w in workspaces)
            Workspaces.Add(ToViewModel(w));
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        if (value is not null)
            value.IsPathMissing = !Directory.Exists(value.Path);
    }

    [RelayCommand]
    public async Task AddWorkspaceAsync(AddWorkspaceDialogViewModel dialogVm)
    {
        var workspace = await _mediator.Send(
            new CreateWorkspaceCommand(dialogVm.Name, dialogVm.FolderPath, !dialogVm.IsPickExisting));
        Workspaces.Add(ToViewModel(workspace));
        SelectedWorkspace = Workspaces[^1];
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
        new() { Id = w.Id, Name = w.Name, Path = w.Path, Position = w.Position };
}

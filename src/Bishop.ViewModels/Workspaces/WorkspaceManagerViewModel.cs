using Bishop.App.Services;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.App.Workspaces.RemoveWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceManagerViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IWorkspaceChangeNotifier _notifier;

    public ObservableCollection<WorkspaceManagerItemViewModel> Workspaces { get; } = [];

    public WorkspaceManagerViewModel(ISender mediator, IWorkspaceChangeNotifier notifier)
    {
        _mediator = mediator;
        _notifier = notifier;
    }

    public async Task LoadAsync()
    {
        var workspaces = await _mediator.Send(new ListWorkspacesQuery(IncludeRemoved: true));
        Workspaces.Clear();
        foreach (var w in workspaces)
        {
            Workspaces.Add(new WorkspaceManagerItemViewModel
            {
                Id = w.Id,
                Name = w.Name,
                Path = w.Path,
                IsRemoved = w.IsRemoved,
                RemovedAt = w.RemovedAt,
            });
        }
    }

    public async Task RemoveAsync(Guid id)
    {
        await _mediator.Send(new RemoveWorkspaceCommand(id));
        await LoadAsync();
        _notifier.NotifyChanged();
    }

    public async Task PurgeAsync(Guid id)
    {
        await _mediator.Send(new PurgeWorkspaceCommand(id));
        await LoadAsync();
        _notifier.NotifyChanged();
    }
}

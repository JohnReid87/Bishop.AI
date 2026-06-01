namespace Bishop.App.Services;

internal sealed class WorkspaceChangeNotifier : IWorkspaceChangeNotifier
{
    public event Action? WorkspacesChanged;
    public void NotifyChanged() => WorkspacesChanged?.Invoke();
}

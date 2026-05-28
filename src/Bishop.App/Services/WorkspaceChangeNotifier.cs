namespace Bishop.App.Services;

public sealed class WorkspaceChangeNotifier : IWorkspaceChangeNotifier
{
    public event Action? WorkspacesChanged;
    public void NotifyChanged() => WorkspacesChanged?.Invoke();
}

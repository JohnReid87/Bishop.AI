namespace Bishop.App.Services;

public class WorkspaceChangeNotifier
{
    public event Action? WorkspacesChanged;
    public void NotifyChanged() => WorkspacesChanged?.Invoke();
}

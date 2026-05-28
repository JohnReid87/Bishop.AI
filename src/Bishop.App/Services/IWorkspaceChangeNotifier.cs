namespace Bishop.App.Services;

public interface IWorkspaceChangeNotifier
{
    event Action WorkspacesChanged;
    void NotifyChanged();
}

namespace Bishop.ViewModels.Shared;

/// <summary>
/// Abstraction over UI-thread marshalling. ViewModels depend on this rather than
/// a concrete framework dispatcher so they remain free of Microsoft.UI.* /
/// Windows.UI.* references.
/// </summary>
public interface IUiDispatcher
{
    void TryEnqueue(Action work);

    void TryEnqueue(Func<Task> work);
}

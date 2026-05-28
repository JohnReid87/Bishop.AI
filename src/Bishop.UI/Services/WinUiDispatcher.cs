using Bishop.ViewModels;
using Microsoft.UI.Dispatching;

namespace Bishop.UI.Services;

internal sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    public WinUiDispatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void TryEnqueue(Action work) => _dispatcherQueue.TryEnqueue(() => work());

    public void TryEnqueue(Func<Task> work) => _dispatcherQueue.TryEnqueue(async () => await work());
}

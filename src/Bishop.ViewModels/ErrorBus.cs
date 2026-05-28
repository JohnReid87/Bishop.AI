using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed class ErrorBus : IErrorBus
{
    private readonly IUiDispatcher _dispatcher;

    public ObservableCollection<ErrorNotificationViewModel> Notifications { get; } = [];
    public Action<Exception>? ShowDetailsHandler { get; set; }

    public ErrorBus(IUiDispatcher dispatcher) => _dispatcher = dispatcher;

    public void Report(Exception ex)
    {
        var notification = new ErrorNotificationViewModel(ex, ShowDetailsHandler, Remove);
        _dispatcher.TryEnqueue(() => Notifications.Add(notification));
    }

    private void Remove(ErrorNotificationViewModel notification)
    {
        _dispatcher.TryEnqueue(() => Notifications.Remove(notification));
    }
}

using System.Collections.ObjectModel;
using Bishop.ViewModels.Shared;

namespace Bishop.ViewModels.Errors;

public sealed class ErrorBus : IErrorBus
{
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly Action<Exception>? _showDetailsAction;

    public ObservableCollection<ErrorNotificationViewModel> Notifications { get; } = [];

    public ErrorBus(IUiDispatcher dispatcher, TimeProvider timeProvider, Action<Exception>? showDetailsAction = null)
    {
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _showDetailsAction = showDetailsAction;
    }

    public void Report(Exception ex)
    {
        var notification = new ErrorNotificationViewModel(ex, _showDetailsAction, Remove, _timeProvider);
        _dispatcher.TryEnqueue(() => Notifications.Add(notification));
    }

    private void Remove(ErrorNotificationViewModel notification)
    {
        _dispatcher.TryEnqueue(() => Notifications.Remove(notification));
    }
}

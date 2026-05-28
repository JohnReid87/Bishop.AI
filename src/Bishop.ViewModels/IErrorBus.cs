using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public interface IErrorBus
{
    ObservableCollection<ErrorNotificationViewModel> Notifications { get; }
    void Report(Exception ex);
    Action<Exception>? ShowDetailsHandler { get; set; }
}

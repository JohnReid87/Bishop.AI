using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Errors;

public interface IErrorBus
{
    ObservableCollection<ErrorNotificationViewModel> Notifications { get; }
    void Report(Exception ex);
}

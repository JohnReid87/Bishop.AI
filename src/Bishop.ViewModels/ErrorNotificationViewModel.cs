using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bishop.ViewModels;

public sealed partial class ErrorNotificationViewModel : ObservableObject
{
    public Exception Exception { get; }
    public string Title => "Background error";
    public string Message => Exception.Message;
    public DateTimeOffset Timestamp { get; }

    private readonly Action<Exception>? _showDetails;
    private readonly Action<ErrorNotificationViewModel> _dismiss;

    [ObservableProperty]
    public partial bool IsOpen { get; set; } = true;

    public ErrorNotificationViewModel(
        Exception ex,
        Action<Exception>? showDetails,
        Action<ErrorNotificationViewModel> dismiss,
        TimeProvider timeProvider)
    {
        Exception = ex;
        _showDetails = showDetails;
        _dismiss = dismiss;
        Timestamp = timeProvider.GetLocalNow();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (!value) _dismiss(this);
    }

    [RelayCommand]
    private void ShowDetails() => _showDetails?.Invoke(Exception);
}

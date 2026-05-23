using System.ComponentModel;

namespace Bishop.App.Services.CatMode;

public sealed class CatModeService : ICatModeService
{
    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    public void Toggle() => IsActive = !IsActive;

    public event PropertyChangedEventHandler? PropertyChanged;
}

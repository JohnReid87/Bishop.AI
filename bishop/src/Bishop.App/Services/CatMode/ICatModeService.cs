using System.ComponentModel;

namespace Bishop.App.Services.CatMode;

public interface ICatModeService : INotifyPropertyChanged
{
    bool IsActive { get; }

    void Toggle();
}

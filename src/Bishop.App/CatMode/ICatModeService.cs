using System.ComponentModel;

namespace Bishop.App.CatMode;

public interface ICatModeService : INotifyPropertyChanged
{
    bool IsActive { get; }

    void Toggle();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;

namespace Bishop.ViewModels.Scripts;

public sealed partial class ScriptItemViewModel : ObservableObject
{
    public string Name { get; }
    public string Path { get; }
    private readonly Action<ScriptItemViewModel>? _onDeleted;
    private readonly Action<ProcessStartInfo> _processLauncher;
    private readonly Action<string> _fileDeleter;

    [ObservableProperty]
    public partial string Args { get; set; } = string.Empty;

    public ScriptItemViewModel(
        string name,
        string path,
        Action<ScriptItemViewModel>? onDeleted = null,
        Action<ProcessStartInfo>? processLauncher = null,
        Action<string>? fileDeleter = null)
    {
        Name = name;
        Path = path;
        _onDeleted = onDeleted;
        _processLauncher = processLauncher ?? (psi => Process.Start(psi));
        _fileDeleter = fileDeleter ?? File.Delete;
    }

    [RelayCommand]
    private void Edit()
    {
        _processLauncher(new ProcessStartInfo(Path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void Delete()
    {
        _fileDeleter(Path);
        _onDeleted?.Invoke(this);
    }
}

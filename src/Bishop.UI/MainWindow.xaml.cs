using Bishop.UI.ViewModels;
using Microsoft.UI.Xaml;

namespace Bishop.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
}

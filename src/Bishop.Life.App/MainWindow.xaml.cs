using System;
using Microsoft.UI.Xaml;

namespace Bishop.Life.App;

public sealed partial class MainWindow : Window
{
    private LifePlanHost? _host;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 1000));

        _host = new LifePlanHost(View);
        Closed += OnClosed;
        _ = _host.StartAsync();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _host?.Dispose();
        _host = null;
    }
}

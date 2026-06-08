using System;
using System.IO;
using System.Runtime.InteropServices;
using Bishop.Shared.Layout;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Bishop.Life.App;

public sealed partial class MainWindow : Window
{
    private const string BishopUiWindowTitle = "Bishop.AI";

    private LifePlanHost? _host;

    public MainWindow()
    {
        InitializeComponent();

        SetupTitleBar();
        PositionWindow();

        _host = new LifePlanHost(View);
        Closed += OnClosed;

        if (Content is FrameworkElement root)
        {
            ApplyTheme(root.ActualTheme);
            root.ActualThemeChanged += (sender, _) => ApplyTheme(sender.ActualTheme);
        }

        _ = _host.StartAsync();
    }

    private void SetupTitleBar()
    {
        // Caption buttons left at their defaults so they follow the OS light/dark
        // theme; we only extend the content area into the title bar and set the
        // window icon so the taskbar / alt-tab show the Bishop mark.
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "bishop-ai.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
    }

    private void PositionWindow()
    {
        var fallbackSize = new SizeInt32(960, 1000);

        if (TryGetBishopUiRect(out var targetRect))
        {
            // Size the window before placement so DWM frame-extent math has the
            // right starting dimensions to compensate against.
            AppWindow.Resize(fallbackSize);
            OppositeHalfPositioner.Place(this, targetRect);
            return;
        }

        AppWindow.Resize(fallbackSize);
    }

    private void ApplyTheme(ElementTheme theme)
    {
        var isDark = theme == ElementTheme.Dark;
        _host?.SetTheme(isDark);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _host?.Dispose();
        _host = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    private static bool TryGetBishopUiRect(out RectInt32 rect)
    {
        var hWnd = FindWindowW(null, BishopUiWindowTitle);
        if (hWnd == nint.Zero || !GetWindowRect(hWnd, out var r))
        {
            rect = default;
            return false;
        }
        rect = new RectInt32(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return true;
    }
}

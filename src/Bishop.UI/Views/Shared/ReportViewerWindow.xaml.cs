using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.IO;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Bishop.UI.Views.Shared;

public sealed partial class ReportViewerWindow : Window
{
    public ReportViewerWindow()
    {
        InitializeComponent();
        SetupTitleBar();

        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            AppWindow.Hide();
        };

        App.MainWindow!.AppWindow.Changed += OnMainWindowChanged;
    }

    private void SetupTitleBar()
    {
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 120);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 30, 30, 38);
        AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 40, 40, 50);
        AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "bishop-ai.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        SetTitleBar(AppTitleBar);
    }

    public async Task ShowReport(Uri uri)
    {
        PositionOnOppositeHalf();

        if (!AppWindow.IsVisible)
            AppWindow.Show();

        try
        {
            await ReportWebView.EnsureCoreWebView2Async();
            ReportWebView.CoreWebView2?.Navigate(uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            App.LogExceptionToFile(ex);
        }

        PositionOnOppositeHalf();
        Activate();
    }

    private void OnMainWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!AppWindow.IsVisible) return;
        if (!args.DidPositionChange && !args.DidSizeChange) return;
        PositionOnOppositeHalf();
    }

    private void PositionOnOppositeHalf()
    {
        var mainAppWindow = App.MainWindow!.AppWindow;
        var wa = DisplayArea.GetFromWindowId(mainAppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var mainPos = mainAppWindow.Position;
        var mainSize = mainAppWindow.Size;
        var mainCenterX = mainPos.X + mainSize.Width / 2;
        var mainIsOnLeft = mainCenterX < wa.X + wa.Width / 2;

        var hWnd = WindowNative.GetWindowHandle(this);
        var viewerPos = AppWindow.Position;
        var viewerSize = AppWindow.Size;
        var (fL, fT, fR, fB) = SnapHelper.GetFrameExtents(hWnd, viewerPos.X, viewerPos.Y, viewerSize.Width, viewerSize.Height);

        int x, y, w, h;
        if (mainIsOnLeft)
        {
            x = wa.X + wa.Width / 2 - fL;
            y = wa.Y - fT;
            w = wa.Width / 2 + fL + fR;
            h = wa.Height + fT + fB;
        }
        else
        {
            x = wa.X - fL;
            y = wa.Y - fT;
            w = wa.Width / 2 + fL + fR;
            h = wa.Height + fT + fB;
        }

        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }
}

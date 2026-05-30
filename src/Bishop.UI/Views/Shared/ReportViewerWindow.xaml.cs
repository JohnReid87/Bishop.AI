using Bishop.ViewModels.Shared;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System.IO;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Bishop.UI.Views.Shared;

public sealed partial class ReportViewerWindow : Window
{
    private readonly ReportViewerWindowViewModel _viewModel;
    private Uri? _currentSourceUri;
    private bool _webMessageWired;

    public ReportViewerWindow(ReportViewerWindowViewModel viewModel)
    {
        _viewModel = viewModel;

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
            if (!_webMessageWired && ReportWebView.CoreWebView2 is { } core)
            {
                core.WebMessageReceived += OnWebMessageReceived;
                core.NavigationStarting += OnNavigationStarting;
                _webMessageWired = true;
            }
            _currentSourceUri = uri;
            ReportWebView.CoreWebView2?.Navigate(uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            App.LogExceptionToFile(ex);
        }

        PositionOnOppositeHalf();
        Activate();
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        => await SafeAsync.RunAsync(() =>
            _viewModel.HandleConvertToCardAsync(args.WebMessageAsJson, _currentSourceUri, ReportWebView.XamlRoot));

    private async void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != "bishop" || uri.Host != "card") return;

        args.Cancel = true;

        if (!int.TryParse(uri.AbsolutePath.TrimStart('/'), out var number)) return;

        await SafeAsync.RunAsync(() =>
            _viewModel.HandleOpenCardAsync(number, _currentSourceUri, ReportWebView.XamlRoot));
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

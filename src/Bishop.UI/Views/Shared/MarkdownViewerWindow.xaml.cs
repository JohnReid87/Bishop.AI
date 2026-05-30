using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.IO;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Bishop.UI.Views.Shared;

public sealed partial class MarkdownViewerWindow : Window
{
    public MarkdownViewerWindow()
    {
        InitializeComponent();

        MarkdownContent.Config = new MarkdownConfig
        {
            Themes = new MarkdownThemes
            {
                CodeBlockMargin = new Thickness(0, 4, 0, 4)
            }
        };

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

        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsResizable = false;
    }

    public void ShowContent(string title, string markdownBody)
    {
        TitleText.Text = title;
        Title = title;
        MarkdownContent.Text = markdownBody;
        PositionOnOppositeHalf();

        if (!AppWindow.IsVisible)
            AppWindow.Show();

        // MyCodeBlock (in the MarkdownTextBlock package) iterates lines.Length instead of Count.
        // Markdig allocates StringLine[] with doubling, so a 79-line block gets a 128-entry array
        // → 49 phantom empty Paragraphs render as blank lines at the bottom of every code block.
        TrimCodeBlockTrailingBlanks(MarkdownContent);

        PositionOnOppositeHalf(); // re-snap with accurate DWM frame extents post-show
        Activate();
    }

    private void OnMainWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!AppWindow.IsVisible) return;
        if (!args.DidPositionChange && !args.DidSizeChange) return;
        PositionOnOppositeHalf();
    }

    private static void TrimCodeBlockTrailingBlanks(MarkdownTextBlock control)
    {
        if (VisualTreeHelper.GetChildrenCount(control) == 0) return;
        if (VisualTreeHelper.GetChild(control, 0) is not Grid container) return;
        if (container.Children.Count == 0) return;
        if (container.Children[0] is not RichTextBlock docRtb) return;

        foreach (var block in docRtb.Blocks)
        {
            if (block is not Paragraph para) continue;
            foreach (var inline in para.Inlines)
            {
                if (inline is not InlineUIContainer iuc) continue;
                if (iuc.Child is not Border border) continue;
                if (border.Child is not RichTextBlock codeRtb) continue;

                while (codeRtb.Blocks.Count > 0 &&
                       codeRtb.Blocks[codeRtb.Blocks.Count - 1] is Paragraph last &&
                       last.Inlines.Count == 0)
                {
                    codeRtb.Blocks.RemoveAt(codeRtb.Blocks.Count - 1);
                }
            }
        }
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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Bishop.Shared.Layout;

/// <summary>
/// Snaps a viewer <see cref="Window"/> to the half of its monitor work area
/// opposite a given target rectangle (typically the main Bishop window). Both
/// Bishop.UI viewer dialogs (<c>MarkdownViewerWindow</c>) and the Bishop.Life
/// shell share this so the two stay in lockstep — Bishop.Life links this same
/// file into its build via <c>&lt;Compile Include="..."&gt;</c>.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
internal static class OppositeHalfPositioner
{
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    /// <summary>
    /// Moves <paramref name="viewer"/> to fill the half of its display work area
    /// opposite <paramref name="targetRect"/> (physical screen coords).
    /// </summary>
    public static void Place(Window viewer, RectInt32 targetRect)
    {
        var hWnd = WindowNative.GetWindowHandle(viewer);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var wa = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;

        var targetCenterX = targetRect.X + targetRect.Width / 2;
        var targetIsOnLeft = targetCenterX < wa.X + wa.Width / 2;

        var viewerPos = viewer.AppWindow.Position;
        var viewerSize = viewer.AppWindow.Size;
        var (fL, fT, fR, fB) = GetFrameExtents(hWnd, viewerPos.X, viewerPos.Y, viewerSize.Width, viewerSize.Height);

        var x = targetIsOnLeft ? wa.X + wa.Width / 2 - fL : wa.X - fL;
        var y = wa.Y - fT;
        var w = wa.Width / 2 + fL + fR;
        var h = wa.Height + fT + fB;

        viewer.AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }

    // Invisible DWM shadow insets (physical px); callers add these back to
    // MoveAndResize coords so the visible content rect lines up exactly.
    private static (int left, int top, int right, int bottom) GetFrameExtents(
        nint hWnd, int posX, int posY, int sizeW, int sizeH)
    {
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var vis, Marshal.SizeOf<RECT>()) != 0)
            return (0, 0, 0, 0);
        return (vis.Left - posX, vis.Top - posY, posX + sizeW - vis.Right, posY + sizeH - vis.Bottom);
    }
}

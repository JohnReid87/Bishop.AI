using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Bishop.App.Terminal;
using Microsoft.UI.Windowing;

namespace Bishop.UI;

[SupportedOSPlatform("windows")]
internal static class SnapHelper
{
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    internal static TerminalSnap ComputeSnap()
    {
        var appWindow = App.MainWindow!.AppWindow;
        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var wa = display.WorkArea;

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var visible, Marshal.SizeOf<RECT>()) == 0)
        {
            return TerminalSnap.RemainderFill(
                visible.Left, visible.Top,
                visible.Right - visible.Left, visible.Bottom - visible.Top,
                wa.X, wa.Y, wa.Width, wa.Height);
        }

        // DWM call failed; fall back to logical bounds
        var pos = appWindow.Position;
        var size = appWindow.Size;
        return TerminalSnap.RemainderFill(pos.X, pos.Y, size.Width, size.Height, wa.X, wa.Y, wa.Width, wa.Height);
    }
}

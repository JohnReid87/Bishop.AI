using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Bishop.App.Skills;
using Bishop.App.Services.Terminal;
using Microsoft.UI.Windowing;

namespace Bishop.UI;

[SupportedOSPlatform("windows")]
internal static class SnapHelper
{
#pragma warning disable CA1416
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
        int physX = 0, physY = 0, physW = 0, physH = 0;
        var physAvailable = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var visible, Marshal.SizeOf<RECT>()) == 0;
        if (physAvailable)
        {
            physX = visible.Left; physY = visible.Top;
            physW = visible.Right - visible.Left; physH = visible.Bottom - visible.Top;
        }

        var pos = appWindow.Position;
        var size = appWindow.Size;
        return SnapComputer.Compute(
            physX, physY, physW, physH, physAvailable,
            pos.X, pos.Y, size.Width, size.Height,
            wa.X, wa.Y, wa.Width, wa.Height);
    }
#pragma warning restore CA1416
}

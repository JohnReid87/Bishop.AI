using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Bishop.App.Services.Terminal;

[SupportedOSPlatform("windows")]
public sealed class TerminalLauncher : ITerminalLauncher
{
    private const string WtWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";
    private const string PsWindowClass = "ConsoleWindowClass";
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private readonly Func<string, bool> _fileExists;
    private readonly Action<ProcessStartInfo> _startProcess;

    public TerminalLauncher() : this(File.Exists, psi => Process.Start(psi)) { }

    internal TerminalLauncher(Func<string, bool> fileExists, Action<ProcessStartInfo> startProcess)
    {
        _fileExists = fileExists;
        _startProcess = startProcess;
    }

    public bool Launch(string workingDirectory, string? claudeArgs, TerminalSnap? snap, string? modelId = null)
    {
        var fullPath = BuildFullPath();
        var wt = FindWindowsTerminal();

        if (wt is not null)
        {
            var psi = new ProcessStartInfo { FileName = wt, UseShellExecute = false };
            // cmd.exe /k resolves claude.cmd; wt -- claude fails because wt uses
            // CreateProcess directly, which cannot execute .cmd wrapper scripts.
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(workingDirectory);
            psi.ArgumentList.Add("cmd.exe");
            psi.ArgumentList.Add("/k");
            psi.ArgumentList.Add("claude");
            if (modelId is not null) { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(modelId); }
            if (claudeArgs is not null) psi.ArgumentList.Add(claudeArgs);
            psi.Environment["PATH"] = fullPath;
            var before = snap.HasValue ? GetWindowsOfClass(WtWindowClass) : null;
            _startProcess(psi);
            if (snap.HasValue) SnapLater(WtWindowClass, snap.Value, before!);
            return true;
        }

        var psFallback = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        psFallback.ArgumentList.Add("-NoExit");
        psFallback.ArgumentList.Add("-Command");
        psFallback.ArgumentList.Add("claude");
        if (modelId is not null) { psFallback.ArgumentList.Add("--model"); psFallback.ArgumentList.Add(modelId); }
        if (claudeArgs is not null) psFallback.ArgumentList.Add(claudeArgs);
        psFallback.Environment["PATH"] = fullPath;
        var psBefore = snap.HasValue ? GetWindowsOfClass(PsWindowClass) : null;
        _startProcess(psFallback);
        if (snap.HasValue) SnapLater(PsWindowClass, snap.Value, psBefore!);
        return false;
    }

    public bool LaunchCommand(string workingDirectory, string command, string[] args, TerminalSnap? snap)
    {
        var fullPath = BuildFullPath();
        var wt = FindWindowsTerminal();

        if (wt is not null)
        {
            var psi = new ProcessStartInfo { FileName = wt, UseShellExecute = false };
            // cmd.exe /k mirrors the Launch path so .cmd / .bat wrappers resolve too.
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(workingDirectory);
            psi.ArgumentList.Add("cmd.exe");
            psi.ArgumentList.Add("/k");
            psi.ArgumentList.Add(command);
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment["PATH"] = fullPath;
            var before = snap.HasValue ? GetWindowsOfClass(WtWindowClass) : null;
            _startProcess(psi);
            if (snap.HasValue) SnapLater(WtWindowClass, snap.Value, before!);
            return true;
        }

        var psFallback = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        psFallback.ArgumentList.Add("-NoExit");
        psFallback.ArgumentList.Add("-Command");
        psFallback.ArgumentList.Add(command);
        foreach (var a in args) psFallback.ArgumentList.Add(a);
        psFallback.Environment["PATH"] = fullPath;
        var psBefore = snap.HasValue ? GetWindowsOfClass(PsWindowClass) : null;
        _startProcess(psFallback);
        if (snap.HasValue) SnapLater(PsWindowClass, snap.Value, psBefore!);
        return false;
    }

    public bool LaunchPlain(string workingDirectory, TerminalSnap? snap)
    {
        var fullPath = BuildFullPath();
        var wt = FindWindowsTerminal();
        var shell = HasPwsh(fullPath) ? "pwsh.exe" : "powershell.exe";

        if (wt is not null)
        {
            var psi = new ProcessStartInfo { FileName = wt, UseShellExecute = false };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(workingDirectory);
            psi.ArgumentList.Add(shell);
            psi.Environment["PATH"] = fullPath;
            var before = snap.HasValue ? GetWindowsOfClass(WtWindowClass) : null;
            _startProcess(psi);
            if (snap.HasValue) SnapLater(WtWindowClass, snap.Value, before!);
            return true;
        }

        var psFallback = new ProcessStartInfo
        {
            FileName = shell,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        psFallback.ArgumentList.Add("-NoExit");
        psFallback.Environment["PATH"] = fullPath;
        var psBefore = snap.HasValue ? GetWindowsOfClass(PsWindowClass) : null;
        _startProcess(psFallback);
        if (snap.HasValue) SnapLater(PsWindowClass, snap.Value, psBefore!);
        return false;
    }

    private bool HasPwsh(string fullPath)
    {
        foreach (var segment in fullPath.Split(';'))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), "pwsh.exe");
                if (_fileExists(candidate)) return true;
            }
            catch (ArgumentException) { }
        }
        return false;
    }

    private static void SnapLater(string windowClass, TerminalSnap snap, HashSet<nint> before)
    {
        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            nint found = 0;
            while (DateTime.UtcNow < deadline)
            {
                foreach (var hWnd in GetWindowsOfClass(windowClass))
                {
                    if (!before.Contains(hWnd)) { found = hWnd; break; }
                }
                if (found != 0) break;
                await Task.Delay(100);
            }

            if (found == 0) return;

            ApplySnap(found, snap);
        });
    }

    private static void ApplySnap(nint hWnd, TerminalSnap snap)
    {
        // Windows adds invisible DWM border pixels outside the visible window edge.
        // GetWindowRect gives the logical rect (including invisible border); DwmGetWindowAttribute
        // DWMWA_EXTENDED_FRAME_BOUNDS gives the visible rect. The difference tells us how much to
        // inflate the target rect so the visible edge lands exactly at the snap boundary.
        int x = snap.X, y = snap.Y, w = snap.Width, h = snap.Height;

        if (GetWindowRect(hWnd, out var logical) &&
            DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var visible, Marshal.SizeOf<RECT>()) == 0)
        {
            var left   = visible.Left   - logical.Left;
            var top    = visible.Top    - logical.Top;
            var right  = logical.Right  - visible.Right;
            var bottom = logical.Bottom - visible.Bottom;
            x -= left;
            y -= top;
            w += left + right;
            h += top + bottom;
        }

        SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static HashSet<nint> GetWindowsOfClass(string className)
    {
        var result = new HashSet<nint>();
        EnumWindows((hWnd, _) =>
        {
            var sb = new StringBuilder(256);
            if (GetClassName(hWnd, sb, sb.Capacity) > 0 && sb.ToString() == className)
                result.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private string? FindWindowsTerminal()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        if (_fileExists(alias)) return alias;

        foreach (var segment in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), "wt.exe");
                if (_fileExists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }

        return null;
    }

    private static string BuildFullPath()
    {
        using var machineEnv = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        using var userEnv = Registry.CurrentUser.OpenSubKey(@"Environment");

        return BuildFullPath(
            machineEnv?.GetValue("Path", "") as string,
            userEnv?.GetValue("Path", "") as string);
    }

    internal static string BuildFullPath(string? machinePath, string? userPath)
    {
        var machine = Environment.ExpandEnvironmentVariables(machinePath ?? "");
        var user = Environment.ExpandEnvironmentVariables(userPath ?? "");
        return CombinePaths(machine, user);
    }

    private static string CombinePaths(string machine, string user) =>
        string.IsNullOrEmpty(user) ? machine : $"{machine};{user}";

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}

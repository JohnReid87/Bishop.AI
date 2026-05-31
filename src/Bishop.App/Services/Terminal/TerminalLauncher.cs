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
    private readonly TimeProvider _timeProvider;

    public TerminalLauncher(TimeProvider timeProvider) : this(File.Exists, psi => Process.Start(psi), timeProvider) { }

    internal TerminalLauncher(Func<string, bool> fileExists, Action<ProcessStartInfo> startProcess, TimeProvider? timeProvider = null)
    {
        _fileExists = fileExists;
        _startProcess = startProcess;
        // Stryker disable once Statement : production callers never pass null; the default-binding path is exercised by the composition root, not unit tests.
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool Launch(string workingDirectory, string? claudeArgs, TerminalSnap? snap, string? modelId = null)
    {
        // cmd.exe /k resolves claude.cmd; wt -- claude fails because wt uses
        // CreateProcess directly, which cannot execute .cmd wrapper scripts.
        return LaunchCore(
            workingDirectory,
            snap,
            wtArgs: psi =>
            {
                psi.ArgumentList.Add("cmd.exe");
                psi.ArgumentList.Add("/k");
                psi.ArgumentList.Add("claude");
                AppendModelAndArgs(psi, modelId, claudeArgs);
            },
            fallbackShell: "powershell.exe",
            fallbackArgs: psi =>
            {
                psi.ArgumentList.Add("-NoExit");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add("claude");
                AppendModelAndArgs(psi, modelId, claudeArgs);
            });
    }

    public bool LaunchCommand(string workingDirectory, string command, string[] args, TerminalSnap? snap)
    {
        // cmd.exe /k mirrors the Launch path so .cmd / .bat wrappers resolve too.
        return LaunchCore(
            workingDirectory,
            snap,
            wtArgs: psi =>
            {
                psi.ArgumentList.Add("cmd.exe");
                psi.ArgumentList.Add("/k");
                psi.ArgumentList.Add(command);
                foreach (var a in args) psi.ArgumentList.Add(a);
            },
            fallbackShell: "powershell.exe",
            fallbackArgs: psi =>
            {
                psi.ArgumentList.Add("-NoExit");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(command);
                foreach (var a in args) psi.ArgumentList.Add(a);
            });
    }

    public bool LaunchPlain(string workingDirectory, TerminalSnap? snap)
    {
        var fullPath = BuildFullPath();
        var shell = HasPwsh(fullPath) ? "pwsh.exe" : "powershell.exe";

        return LaunchCore(
            workingDirectory,
            snap,
            wtArgs: psi => psi.ArgumentList.Add(shell),
            fallbackShell: shell,
            fallbackArgs: psi => psi.ArgumentList.Add("-NoExit"),
            precomputedPath: fullPath);
    }

    private bool LaunchCore(
        string workingDirectory,
        TerminalSnap? snap,
        Action<ProcessStartInfo> wtArgs,
        string fallbackShell,
        Action<ProcessStartInfo> fallbackArgs,
        string? precomputedPath = null)
    {
        var fullPath = precomputedPath ?? BuildFullPath();
        var wt = FindWindowsTerminal();

        if (wt is not null)
        {
            var psi = new ProcessStartInfo { FileName = wt, UseShellExecute = false };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(workingDirectory);
            wtArgs(psi);
            psi.Environment["PATH"] = fullPath;
            StartWithSnap(psi, WtWindowClass, snap);
            return true;
        }

        var psFallback = new ProcessStartInfo
        {
            FileName = fallbackShell,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        fallbackArgs(psFallback);
        psFallback.Environment["PATH"] = fullPath;
        StartWithSnap(psFallback, PsWindowClass, snap);
        return false;
    }

    private void StartWithSnap(ProcessStartInfo psi, string windowClass, TerminalSnap? snap)
    {
        // Stryker disable once Statement : snap-path is deliberately untested (see TerminalLauncherTests.cs:633-638).
        var before = snap.HasValue ? GetWindowsOfClass(windowClass) : null;
        _startProcess(psi);
        // Stryker disable once Statement : snap-path is deliberately untested (see TerminalLauncherTests.cs:633-638).
        if (snap.HasValue) SnapLater(windowClass, snap.Value, before!);
    }

    private static void AppendModelAndArgs(ProcessStartInfo psi, string? modelId, string? claudeArgs)
    {
        if (modelId is not null) { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(modelId); }
        if (claudeArgs is not null) psi.ArgumentList.Add(claudeArgs);
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
            catch (ArgumentException) { } // invalid path chars in this PATH segment — skip it
        }
        return false;
    }

    private void SnapLater(string windowClass, TerminalSnap snap, HashSet<nint> before)
    {
        var timeProvider = _timeProvider;
        _ = Task.Run(async () =>
        {
            var found = await PollForNewWindowAsync(
                () => GetWindowsOfClass(windowClass),
                before,
                timeProvider);

            if (found == 0) return;

            ApplySnap(found, snap);
            await Task.Delay(500);
            ApplySnap(found, snap);
        });
    }

    internal static async Task<nint> PollForNewWindowAsync(
        Func<HashSet<nint>> getWindows,
        HashSet<nint> before,
        TimeProvider timeProvider,
        TimeSpan? pollInterval = null)
    {
        var deadline = timeProvider.GetUtcNow().AddSeconds(3);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        nint found = 0;
        while (timeProvider.GetUtcNow() < deadline)
        {
            foreach (var hWnd in getWindows())
            {
                if (!before.Contains(hWnd)) { found = hWnd; break; }
            }
            if (found != 0) break;
            await Task.Delay(interval);
        }
        return found;
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

    // Stryker disable all : snap-path infrastructure depending on real Win32 EnumWindows/GetClassName; deliberately untested (see TerminalLauncherTests.cs:633-638).
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
    // Stryker restore all

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
            catch (ArgumentException) { } // invalid path chars in this PATH segment — skip it
        }

        return null;
    }

    // Stryker disable all : registry-coupled I/O; killing literal mutants here requires a registry seam — tracked as a follow-up refactor. The pure overload below is fully tested.
    private static string BuildFullPath()
    {
        using var machineEnv = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        using var userEnv = Registry.CurrentUser.OpenSubKey(@"Environment");

        return BuildFullPath(
            machineEnv?.GetValue("Path", "") as string,
            userEnv?.GetValue("Path", "") as string);
    }
    // Stryker restore all

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

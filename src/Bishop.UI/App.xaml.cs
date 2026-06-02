using Bishop.App;
using Bishop.UI.Services;
using Bishop.UI.Views.Shared;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Bishop.UI;

public partial class App : Application
{
    private IHost _host = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    public static MainWindow? MainWindow { get; private set; }

    public static MarkdownViewerWindow? MarkdownViewer { get; private set; }

    public static ReportViewerWindow? ReportViewer { get; private set; }

    private static volatile bool _isMainWindowClosed;
    private static int _showingErrorDialog;
    private static readonly object _logLock = new();
    private static string? _lastLogKey;
    private static int _lastLogCount;
    private const int MaxConsecutiveSameLog = 10;
    private const long MaxLogFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static TimeProvider _timeProvider = TimeProvider.System;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var connStr = GetConnectionString();
        var dbPath = connStr["Data Source=".Length..];
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddBishopApp(connStr);
                services.AddSingleton(sp => new DbChangeWatcher(
                    dbPath,
                    sp.GetRequiredService<ILogger<DbChangeWatcher>>(),
                    sp.GetRequiredService<ISafeAsyncRunner>()));
                services.AddSingleton<IUiDispatcher>(_ => new WinUiDispatcher(dispatcherQueue));
                services.AddSingleton<IErrorBus>(sp => new ErrorBus(
                    sp.GetRequiredService<IUiDispatcher>(),
                    sp.GetRequiredService<TimeProvider>(),
                    ex =>
                    {
                        var root = MainWindow?.Content?.XamlRoot;
                        if (root is not null)
                            _ = ErrorDialog.ShowAsync(root, ex);
                    }));
                services.AddSingleton<ISafeAsyncRunner>(sp => new SafeAsyncRunner(
                    sp.GetRequiredService<ILogger<SafeAsyncRunner>>(),
                    onException: ex =>
                    {
                        LogExceptionToFile(ex);
                        sp.GetRequiredService<IErrorBus>().Report(ex);
                    }));
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ICardDetailDialogService, CardDetailDialogService>();
                services.AddTransient<SettingsDialogViewModel>();
                services.AddTransient<FindingsViewModel>();
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<WorkspaceBoardViewModel>();
                services.AddTransient<CommitsFlyoutViewModel>();
                services.AddTransient<GitConfigViewModel>();
                // Transient + IDisposable: WorkspaceDetailPage.OnNavigatedFrom owns disposal.
                services.AddTransient<WorkspaceNotesViewModel>();
                services.AddTransient<WorkspaceMonitoringViewModel>();
                services.AddTransient<WorkspaceManagerViewModel>();
                services.AddTransient<BishopSettingsViewModel>();
                services.AddTransient<WorkspaceBatchesViewModel>();
                services.AddTransient<ScriptsPageViewModel>();
            })
            .Build();

        _host.Start();
        Services = _host.Services;
        _timeProvider = Services.GetRequiredService<TimeProvider>();

        UnhandledException += OnAppUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        MainWindow = new MainWindow(_host.Services.GetRequiredService<MainWindowViewModel>());
        MainWindow.Closed += (_, _) => _isMainWindowClosed = true;
        MainWindow.Activate();

        MarkdownViewer = new MarkdownViewerWindow();
        ReportViewer = new ReportViewerWindow();
    }

    // async void event handler — required by the UnhandledException signature.
    // Exception flow: any throw inside HandleUnhandledExceptionAsync is caught by
    // ISafeAsyncRunner.RunAsync, logged via its injected ILogger, and routed
    // through the onException callback wired in OnLaunched. Anything that
    // escapes RunAsync would terminate the process, so the helper must never
    // throw synchronously before the inner try.
    private static async void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        await HandleUnhandledExceptionAsync(e);
    }

    private static Task HandleUnhandledExceptionAsync(Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => Services.GetRequiredService<ISafeAsyncRunner>().RunAsync(async () =>
        {
            e.Handled = true;
            var ex = e.Exception;
            LogExceptionToFile(ex);

            if (_isMainWindowClosed) return;

            var root = MainWindow?.Content?.XamlRoot;
            if (root is null) return;

            if (Interlocked.CompareExchange(ref _showingErrorDialog, 1, 0) != 0) return;
            try
            {
                await ErrorDialog.ShowAsync(root, ex);
            }
            finally
            {
                Interlocked.Exchange(ref _showingErrorDialog, 0);
            }
        });

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        LogExceptionToFile(e.Exception);
        Services.GetRequiredService<IErrorBus>().Report(e.Exception);
    }

    internal static void LogExceptionToFile(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bishop.AI");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "bishop-errors.log");

            var key = $"{ex.GetType().FullName}:{ex.Message}";
            lock (_logLock)
            {
                if (key == _lastLogKey)
                {
                    _lastLogCount++;
                    if (_lastLogCount == MaxConsecutiveSameLog)
                        File.AppendAllText(logPath,
                            $"  (suppressing further identical entries){Environment.NewLine}");
                    if (_lastLogCount >= MaxConsecutiveSameLog) return;
                }
                else
                {
                    _lastLogKey = key;
                    _lastLogCount = 1;
                }

                // Rotate at cap — handles both runaway loops and the existing 6.67 GB file
                var fi = new FileInfo(logPath);
                if (fi.Exists && fi.Length > MaxLogFileSizeBytes)
                    File.Move(logPath, logPath + ".bak", overwrite: true);

                File.AppendAllText(logPath,
                    $"[{_timeProvider.GetLocalNow():yyyy-MM-dd HH:mm:ss zzz}] {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}");
            }
        }
        catch (Exception logEx)
        {
            // intentional: file logging is best-effort; failure must not crash the error handler
            Debug.WriteLine($"[Bishop] LogExceptionToFile failed: {logEx.GetType().FullName}: {logEx.Message}");
        }

        Debug.WriteLine($"[Bishop] Unhandled exception: {ex.GetType().FullName}: {ex.Message}");
    }

    private static string GetConnectionString() => BishopDbConnectionString.Resolve();
}

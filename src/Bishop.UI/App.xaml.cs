using Bishop.App;
using Bishop.UI.Services;
using Bishop.UI.Views;
using Bishop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI;

public partial class App : Application
{
    private IHost _host = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    public static MainWindow? MainWindow { get; private set; }

    public static MarkdownViewerWindow? MarkdownViewer { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SafeAsync.OnException = LogExceptionToFile;

        var connStr = GetConnectionString();
        var dbPath = connStr["Data Source=".Length..];
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddBishopApp(connStr, BishopStampPath.Resolve());
                services.AddSingleton(_ => new DbChangeWatcher(dbPath));
                services.AddSingleton<IUiDispatcher>(_ => new WinUiDispatcher(dispatcherQueue));
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<WorkspaceBoardViewModel>();
                services.AddTransient<WorkspaceNotesViewModel>();
                services.AddTransient<WorkspaceMonitoringViewModel>();
                services.AddTransient<WorkspaceManagerViewModel>();
                services.AddTransient<BishopSettingsViewModel>();
            })
            .Build();

        _host.Start();
        Services = _host.Services;

        UnhandledException += OnAppUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        MainWindow = new MainWindow(_host.Services.GetRequiredService<MainWindowViewModel>());
        MainWindow.Activate();

        MarkdownViewer = new MarkdownViewerWindow();
    }

    private static async void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            e.Handled = true;
            var ex = e.Exception;
            LogExceptionToFile(ex);

            var root = MainWindow?.Content?.XamlRoot;
            if (root is null) return;

            var dialog = new ContentDialog
            {
                Title = "I'm sorry, Dave. I'm afraid I can't do that.",
                Content = ExceptionDialogHelper.BuildErrorDialogText(ex),
                PrimaryButtonText = "Copy details",
                SecondaryButtonText = "Open log folder",
                CloseButtonText = "Dismiss",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root,
            };
            try
            {
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var pkg = new DataPackage();
                    pkg.SetText($"{ex.GetType().FullName}\n{ex.Message}\n\n{ex}");
                    Clipboard.SetContent(pkg);
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    var logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Bishop.AI");
                    Process.Start("explorer.exe", logDir);
                }
            }
            catch (COMException)
            {
                // ShowAsync throws COMException when a ContentDialog is already open.
                // The error is already logged above; swallow to prevent a cascading crash.
            }
            catch (Exception showEx)
            {
                LogExceptionToFile(showEx);
            }
        });

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        LogExceptionToFile(e.Exception);
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
            File.AppendAllText(logPath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {ex}{Environment.NewLine}");
        }
        catch { }

        Debug.WriteLine($"[Bishop] Unhandled exception: {ex}");
    }

    private static string GetConnectionString() => BishopDbConnectionString.Resolve();
}

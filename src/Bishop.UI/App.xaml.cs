using Bishop.App;
using Bishop.UI.Services;
using Bishop.UI.ViewModels;
using Bishop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;

namespace Bishop.UI;

public partial class App : Application
{
    private IHost _host = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    public static MainWindow? MainWindow { get; private set; }

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
                services.AddBishopApp(connStr, BishopStampPath.Resolve());
                services.AddSingleton(_ => new DbChangeWatcher(dbPath));
                services.AddSingleton<IUiDispatcher>(_ => new WinUiDispatcher(dispatcherQueue));
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<WorkspaceBoardViewModel>();
                services.AddTransient<WorkspaceNotesViewModel>();
                services.AddTransient<SkillViewerViewModel>();
            })
            .Build();

        _host.Start();
        Services = _host.Services;

        UnhandledException += OnAppUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        MainWindow = new MainWindow(_host.Services.GetRequiredService<MainWindowViewModel>());
        MainWindow.Activate();
    }

    private static async void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        var ex = e.Exception;
        LogExceptionToFile(ex);

        var root = MainWindow?.Content?.XamlRoot;
        if (root is null) return;

        var dialog = new ContentDialog
        {
            Title = "Unexpected Error",
            Content = $"{ex.GetType().Name}: {ex.Message}",
            CloseButtonText = "OK",
            XamlRoot = root,
        };
        await dialog.ShowAsync();
    }

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

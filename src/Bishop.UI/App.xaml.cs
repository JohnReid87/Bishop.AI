using Bishop.App;
using Bishop.UI.Services;
using Bishop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

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

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddBishopApp(connStr);
                services.AddSingleton(_ => new DbChangeWatcher(dbPath));
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<WorkspaceBoardViewModel>();
                services.AddTransient<WorkspaceNotesViewModel>();
            })
            .Build();

        _host.Start();
        Services = _host.Services;

        MainWindow = new MainWindow(_host.Services.GetRequiredService<MainWindowViewModel>());
        MainWindow.Activate();
    }

    private static string GetConnectionString() => BishopDbConnectionString.Resolve();
}

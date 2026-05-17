using Bishop.App;
using Bishop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace Bishop.UI;

public partial class App : Application
{
    private IHost _host = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddBishopApp(GetConnectionString());
                services.AddTransient<MainWindowViewModel>();
            })
            .Build();

        _host.Start();
        Services = _host.Services;

        var window = new MainWindow(_host.Services.GetRequiredService<MainWindowViewModel>());
        window.Activate();
    }

    private static string GetConnectionString()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.IO");
        Directory.CreateDirectory(dir);
        return $"Data Source={Path.Combine(dir, "bishop.db")}";
    }
}

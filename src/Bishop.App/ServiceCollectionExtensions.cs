using System.Diagnostics.CodeAnalysis;
using Bishop.App.Services;
using Bishop.App.Services.CatMode;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.App.Ping;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBishopApp(this IServiceCollection services, string dbConnectionString, string stampPath)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingQueryHandler).Assembly));
        services.AddDbContextFactory<BishopDbContext>(options =>
            options.UseSqlite(dbConnectionString)
                   .AddInterceptors(new SqliteForeignKeyInterceptor()));
        services.AddHostedService(sp => new DatabaseInitializer(
            sp.GetRequiredService<IDbContextFactory<BishopDbContext>>(),
            stampPath));
        services.AddSingleton<IGitCli, GitCli>();
        services.AddSingleton<IGhCli, GhCli>();
        services.AddSingleton<IClaudeExecutableResolver, ClaudeExecutableResolver>();
        services.AddSingleton<IClaudeCliRunner, ClaudeCliRunner>();
        services.AddSingleton<IAppSettings, AppSettingsService>();
#pragma warning disable CA1416 // Bishop.AI is Windows-only; TerminalLauncher requires Windows APIs
        services.AddSingleton<ITerminalLauncher, TerminalLauncher>();
#pragma warning restore CA1416
        services.AddSingleton<IWorkspaceContextSeeder, WorkspaceContextSeeder>();
        services.AddSingleton<ICatModeService, CatModeService>();
        services.AddSingleton<IWorkspaceChangeNotifier, WorkspaceChangeNotifier>();
        return services;
    }
}

using System.Diagnostics.CodeAnalysis;
using Bishop.App.CatMode;
using Bishop.App.Claude;
using Bishop.App.Git;
using Bishop.App.GitHub;
using Bishop.App.Ping;
using Bishop.App.Settings;
using Bishop.App.Tags;
using Bishop.App.Terminal;
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
            options.UseSqlite(dbConnectionString));
        services.AddHostedService(sp => new DatabaseInitializer(
            sp.GetRequiredService<IDbContextFactory<BishopDbContext>>(),
            sp.GetRequiredService<IDefaultTagSeeder>(),
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
        services.AddSingleton<IDefaultTagSeeder, DefaultTagSeeder>();
        services.AddSingleton<ICatModeService, CatModeService>();
        return services;
    }
}

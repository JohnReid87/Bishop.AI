using System.Diagnostics.CodeAnalysis;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
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
        services.AddSingleton<IBatchRepository, BatchRepository>();
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
        services.AddSingleton<IContextProvider, WorkOnCardContextProvider>();
        services.AddSingleton<IContextProvider, AutoCardContextProvider>();
        services.AddSingleton<IContextProvider, CoverageContextProvider>();
        services.AddSingleton<IContextProvider, ArchContextProvider>();
        services.AddSingleton<IContextProvider, SecurityContextProvider>();
        services.AddSingleton<IContextProvider, TestsContextProvider>();
        services.AddSingleton<IContextProvider, AuditDocsContextProvider>();
        services.AddSingleton<IContextProvider, GrillMeContextProvider>();
        services.AddSingleton<IContextProvider, TriageContextProvider>();
        services.AddSingleton<IContextProvider, ChatContextProvider>();
        return services;
    }
}

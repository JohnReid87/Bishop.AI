using System.Diagnostics.CodeAnalysis;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Services;
using Bishop.App.Services.CatMode;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using Bishop.App.Services.Settings;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.App.Services.Terminal;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBishopApp(this IServiceCollection services, string dbConnectionString)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetWorkspaceQueryHandler).Assembly));
        services.AddDbContextFactory<BishopDbContext>(options =>
            options.UseSqlite(dbConnectionString)
                   .AddInterceptors(new SqliteForeignKeyInterceptor()));
        services.AddHostedService(sp => new DatabaseInitializer(
            sp.GetRequiredService<IDbContextFactory<BishopDbContext>>(),
            dbConnectionString));
        services.AddHostedService<WorkspaceSkillRunCleanup>();
        services.AddSingleton<IGitCli, GitCli>();
        services.AddSingleton<IClaudeCliRunner, ClaudeCliRunner>();
        services.AddSingleton<IAppSettings, AppSettingsService>();
#pragma warning disable CA1416 // Bishop.AI is Windows-only; TerminalLauncher requires Windows APIs
        services.AddSingleton<ITerminalLauncher, TerminalLauncher>();
#pragma warning restore CA1416
        services.AddSingleton<IWorkspaceContextSeeder, WorkspaceContextSeeder>();
        services.AddSingleton<ICatModeService, CatModeService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceChangeNotifier>();
        services.AddSingleton<IContextProvider, WorkOnCardContextProvider>();
        services.AddSingleton<IContextProvider, AutoCardContextProvider>();
        services.AddSingleton<IContextProvider, CoverageContextProvider>();
        services.AddSingleton<IContextProvider, MutationContextProvider>();
        services.AddSingleton<IContextProvider, ArchContextProvider>();
        services.AddSingleton<IContextProvider, DeadCodeContextProvider>();
        services.AddSingleton<IContextProvider, SecurityContextProvider>();
        services.AddSingleton<IContextProvider, TestsContextProvider>();
        services.AddSingleton<IContextProvider, AuditDocsContextProvider>();
        services.AddSingleton<IContextProvider, GrillCardsContextProvider>();
        services.AddSingleton<IContextProvider, SpecCardsContextProvider>();
        services.AddSingleton<IContextProvider, GrillDocsContextProvider>();
        services.AddSingleton<IContextProvider, TriageContextProvider>();
        return services;
    }
}

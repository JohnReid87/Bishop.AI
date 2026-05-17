using Bishop.App.Ping;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBishopApp(this IServiceCollection services, string dbConnectionString)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingQueryHandler).Assembly));
        services.AddDbContext<BishopDbContext>(options =>
            options.UseSqlite($"{dbConnectionString};Journal Mode=WAL"));
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}

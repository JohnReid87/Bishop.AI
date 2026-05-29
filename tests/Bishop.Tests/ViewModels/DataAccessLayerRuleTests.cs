using System.Reflection;
using Bishop.App.Ping;
using Bishop.Data;
using FluentAssertions;
using MediatR;

namespace Bishop.Tests.ViewModels;

/// <summary>
/// Enforces the data-access contract documented in CONTEXT.md: handlers in Bishop.App
/// use IDbContextFactory&lt;BishopDbContext&gt; directly — no Repository abstraction allowed.
/// </summary>
public class DataAccessLayerRuleTests
{
    private static readonly Assembly DataAssembly = typeof(BishopDbContext).Assembly;
    private static readonly Assembly AppAssembly = typeof(PingQueryHandler).Assembly;

    [Fact]
    public void BishopData_ContainsNoRepositoryTypes()
    {
        var repositoryTypes = DataAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        repositoryTypes
            .Should()
            .BeEmpty("direct IDbContextFactory<BishopDbContext> is the only data-access pattern — Repository types are not permitted in Bishop.Data");
    }

    [Fact]
    public void BishopApp_HandlersDoNotInjectRepositories()
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        var violations = AppAssembly.GetTypes()
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
            .Where(t => t.GetConstructors()
                .Any(ctor => ctor.GetParameters()
                    .Any(p => p.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal))))
            .Select(t => t.FullName)
            .ToList();

        violations
            .Should()
            .BeEmpty("handlers in Bishop.App must not inject Repository types — use IDbContextFactory<BishopDbContext> directly");
    }
}

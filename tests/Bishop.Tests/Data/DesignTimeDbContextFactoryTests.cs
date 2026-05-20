using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Data;

public sealed class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsContextConfiguredWithDesignTimeConnectionString()
    {
        var factory = new DesignTimeDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
        context.Database.GetDbConnection().ConnectionString.Should().Be("Data Source=bishop-design.db");
    }
}

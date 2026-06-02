using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Data;

public sealed class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsContextWithAbsoluteTempPathDataSource()
    {
        // Arrange
        var factory = new DesignTimeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        var connectionString = context.Database.GetDbConnection().ConnectionString;
        var dataSource = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .First(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            .Substring("Data Source=".Length);

        Path.IsPathRooted(dataSource).Should().BeTrue("design-time DB must use an absolute path to avoid creating files in the working directory");
        dataSource.Should().StartWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), "design-time DB must live under the system temp directory");
    }
}

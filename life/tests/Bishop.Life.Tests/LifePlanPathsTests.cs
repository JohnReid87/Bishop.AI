using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifePlanPathsTests
{
    [Fact]
    public void Resolve_WithoutOverride_PointsAtAppDataBishopLife()
    {
        var saved = Environment.GetEnvironmentVariable(LifePlanPaths.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, null);

            var path = LifePlanPaths.Resolve();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path.Should().Be(Path.Combine(appData, "Bishop", "life", "bishop.life.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, saved);
        }
    }

    [Fact]
    public void Resolve_WithAbsoluteOverride_ReturnsOverride()
    {
        var saved = Environment.GetEnvironmentVariable(LifePlanPaths.EnvVarName);
        try
        {
            var target = Path.Combine(Path.GetTempPath(), "bishop-life-override.json");
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, target);

            LifePlanPaths.Resolve().Should().Be(target);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, saved);
        }
    }

    [Fact]
    public void ResolveGoogleTokenPath_SitsAlongsideLifeFile()
    {
        var saved = Environment.GetEnvironmentVariable(LifePlanPaths.EnvVarName);
        try
        {
            var target = Path.Combine(Path.GetTempPath(), "bishop-life-override.json");
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, target);

            LifePlanPaths.ResolveGoogleTokenPath()
                .Should().Be(Path.Combine(Path.GetTempPath(), "google-token.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, saved);
        }
    }

    [Fact]
    public void Resolve_WithRelativeOverride_Throws()
    {
        var saved = Environment.GetEnvironmentVariable(LifePlanPaths.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, "relative/path.json");

            var act = () => LifePlanPaths.Resolve();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(LifePlanPaths.EnvVarName, saved);
        }
    }
}

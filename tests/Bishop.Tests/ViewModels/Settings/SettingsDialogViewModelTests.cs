using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Settings;

public class SettingsDialogViewModelTests
{
    private static SettingsDialogViewModel Make() => new(null!);

    [Fact]
    public void AppVersion_IsVersionStringOrDash()
    {
        var vm = Make();

        vm.AppVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$|^—$");
    }

    [Fact]
    public void DbPath_StripsDataSourcePrefix()
    {
        var original = Environment.GetEnvironmentVariable("BISHOP_DB");
        try
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "test_bishop.db");
            Environment.SetEnvironmentVariable("BISHOP_DB", dbPath);

            var vm = Make();

            vm.DbPath.Should().Be(dbPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BISHOP_DB", original);
        }
    }

    [Fact]
    public void DbPath_DefaultsToBishopDb_WhenNoEnvVar()
    {
        var original = Environment.GetEnvironmentVariable("BISHOP_DB");
        try
        {
            Environment.SetEnvironmentVariable("BISHOP_DB", null);

            var vm = Make();

            vm.DbPath.Should().EndWith("bishop.db");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BISHOP_DB", original);
        }
    }

    [Fact]
    public void BuildConfiguration_IsKnownValue()
    {
        var vm = Make();

        vm.BuildConfiguration.Should().BeOneOf("Debug", "Release");
    }
}

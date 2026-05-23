using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class SettingsDialogViewModelTests
{
    [Fact]
    public void AppVersion_IsVersionStringOrDash()
    {
        var vm = new SettingsDialogViewModel();

        vm.AppVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$|^—$");
    }

    [Fact]
    public void DbPath_StripsDataSourcePrefix()
    {
        var original = Environment.GetEnvironmentVariable("BISHOP_DB");
        try
        {
            Environment.SetEnvironmentVariable("BISHOP_DB", @"C:\test\bishop.db");

            var vm = new SettingsDialogViewModel();

            vm.DbPath.Should().Be(@"C:\test\bishop.db");
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

            var vm = new SettingsDialogViewModel();

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
        var vm = new SettingsDialogViewModel();

        vm.BuildConfiguration.Should().BeOneOf("Debug", "Release");
    }
}

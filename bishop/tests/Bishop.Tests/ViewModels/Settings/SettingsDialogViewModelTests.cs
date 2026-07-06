using Bishop.App.Services.Settings;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Settings;

public class SettingsDialogViewModelTests
{
    private static SettingsDialogViewModel Make(IAppSettings? appSettings = null) =>
        new(null!, appSettings ?? Substitute.For<IAppSettings>(), new PassThroughSafeAsync());

    private sealed class PassThroughSafeAsync : ISafeAsyncRunner
    {
        public Task RunAsync(Func<Task> action) => action();
    }

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

    [Fact]
    public async Task LoadGeneralAsync_SetsShowHiddenWorkspaces_FromPersistedValue()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("show_hidden_workspaces", Arg.Any<CancellationToken>())
            .Returns("True");
        var vm = Make(appSettings);

        await vm.LoadGeneralAsync();

        vm.ShowHiddenWorkspaces.Should().BeTrue();
    }

    [Fact]
    public async Task LoadGeneralAsync_DefaultsFalse_WhenSettingUnset()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("show_hidden_workspaces", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var vm = Make(appSettings);

        await vm.LoadGeneralAsync();

        vm.ShowHiddenWorkspaces.Should().BeFalse();
    }

    [Fact]
    public async Task LoadGeneralAsync_DoesNotPersist_WhenApplyingLoadedValue()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("show_hidden_workspaces", Arg.Any<CancellationToken>())
            .Returns("True");
        var vm = Make(appSettings);

        await vm.LoadGeneralAsync();

        await appSettings.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ShowHiddenWorkspaces_Toggled_PersistsViaSetAsync()
    {
        var appSettings = Substitute.For<IAppSettings>();
        var vm = Make(appSettings);

        vm.ShowHiddenWorkspaces = true;

        appSettings.Received(1).SetAsync(
            "show_hidden_workspaces", "True", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadGeneralAsync_SetsShowClosedBatches_FromPersistedValue()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("show_closed_batches", Arg.Any<CancellationToken>())
            .Returns("True");
        var vm = Make(appSettings);

        await vm.LoadGeneralAsync();

        vm.ShowClosedBatches.Should().BeTrue();
    }

    [Fact]
    public async Task LoadGeneralAsync_DefaultsShowClosedBatchesFalse_WhenSettingUnset()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("show_closed_batches", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var vm = Make(appSettings);

        await vm.LoadGeneralAsync();

        vm.ShowClosedBatches.Should().BeFalse();
    }

    [Fact]
    public void ShowClosedBatches_Toggled_PersistsViaSetAsync()
    {
        var appSettings = Substitute.For<IAppSettings>();
        var vm = Make(appSettings);

        vm.ShowClosedBatches = true;

        appSettings.Received(1).SetAsync(
            "show_closed_batches", "True", Arg.Any<CancellationToken>());
    }
}

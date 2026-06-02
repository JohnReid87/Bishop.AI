using Bishop.App.Scripts;
using Bishop.App.Scripts.GetScripts;
using Bishop.App.Scripts.LaunchScript;
using Bishop.App.Skills.LaunchSkill;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.Diagnostics;

namespace Bishop.Tests.ViewModels.Scripts;

public class ScriptsPageViewModelTests
{
    [Fact]
    public void Scripts_InitiallyEmpty()
    {
        var sender = Substitute.For<ISender>();

        var vm = new ScriptsPageViewModel(sender);

        vm.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadScriptsAsync_SendsGetScriptsQuery()
    {
        var sender = Substitute.For<ISender>();
        IReadOnlyList<ScriptInfo> empty = [];
        sender.Send(Arg.Any<GetScriptsQuery>(), Arg.Any<CancellationToken>()).Returns(empty);

        var vm = new ScriptsPageViewModel(sender);
        await vm.LoadScriptsAsync();

        await sender.Received(1).Send(Arg.Any<GetScriptsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadScriptsAsync_PopulatesScriptsCollection()
    {
        var sender = Substitute.For<ISender>();
        IReadOnlyList<ScriptInfo> scripts =
        [
            new ScriptInfo("script-one", @"C:\scripts\script-one.ps1"),
            new ScriptInfo("script-two", @"C:\scripts\script-two.ps1"),
        ];
        sender.Send(Arg.Any<GetScriptsQuery>(), Arg.Any<CancellationToken>()).Returns(scripts);

        var vm = new ScriptsPageViewModel(sender);
        await vm.LoadScriptsAsync();

        vm.Scripts.Should().HaveCount(2);
        vm.Scripts[0].Name.Should().Be("script-one");
        vm.Scripts[0].Path.Should().Be(@"C:\scripts\script-one.ps1");
        vm.Scripts[1].Name.Should().Be("script-two");
        vm.Scripts[1].Path.Should().Be(@"C:\scripts\script-two.ps1");
    }

    [Fact]
    public async Task LoadScriptsAsync_ClearsExistingScriptsBeforeRefill()
    {
        var sender = Substitute.For<ISender>();
        IReadOnlyList<ScriptInfo> firstBatch = [new ScriptInfo("old-script", @"C:\s\old.ps1")];
        IReadOnlyList<ScriptInfo> secondBatch = [new ScriptInfo("new-script", @"C:\s\new.ps1")];
        sender.Send(Arg.Any<GetScriptsQuery>(), Arg.Any<CancellationToken>())
            .Returns(firstBatch, secondBatch);

        var vm = new ScriptsPageViewModel(sender);
        await vm.LoadScriptsAsync();
        await vm.LoadScriptsAsync();

        vm.Scripts.Should().HaveCount(1);
        vm.Scripts[0].Name.Should().Be("new-script");
    }

    [Fact]
    public async Task RunScriptAsync_SendsLaunchScriptCommandWithPathAndArgs()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<LaunchScriptCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var script = new ScriptItemViewModel("my-script", @"C:\scripts\my-script.ps1");
        script.Args = "-Verbose";

        var vm = new ScriptsPageViewModel(sender);
        await vm.RunScriptAsync(script);

        await sender.Received(1).Send(
            Arg.Is<LaunchScriptCommand>(c =>
                c.ScriptPath == @"C:\scripts\my-script.ps1" &&
                c.Args == "-Verbose"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunScriptAsync_UsesEmptyArgsByDefault()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<LaunchScriptCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var script = new ScriptItemViewModel("my-script", @"C:\scripts\my-script.ps1");

        var vm = new ScriptsPageViewModel(sender);
        await vm.RunScriptAsync(script);

        await sender.Received(1).Send(
            Arg.Is<LaunchScriptCommand>(c =>
                c.ScriptPath == @"C:\scripts\my-script.ps1" &&
                c.Args == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadScriptsAsync_DeleteCommandRemovesItemFromCollection()
    {
        var sender = Substitute.For<ISender>();
        var tempPath = System.IO.Path.GetTempFileName();
        IReadOnlyList<ScriptInfo> scripts = [new ScriptInfo("s", tempPath)];
        sender.Send(Arg.Any<GetScriptsQuery>(), Arg.Any<CancellationToken>()).Returns(scripts);

        var vm = new ScriptsPageViewModel(sender);
        await vm.LoadScriptsAsync();
        var item = vm.Scripts[0];

        item.DeleteCommand.Execute(null);

        vm.Scripts.Should().BeEmpty();
    }

    [Fact]
    public void OpenFolderCommand_InvokesProcessLauncherWithExplorerAndScriptsFolder()
    {
        var sender = Substitute.For<ISender>();
        ProcessStartInfo? captured = null;
        var vm = new ScriptsPageViewModel(sender, psi => captured = psi);
        var expectedFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "scripts");

        vm.OpenFolderCommand.Execute(null);

        captured.Should().NotBeNull();
        captured!.FileName.Should().Be("explorer.exe");
        captured.Arguments.Should().Be(expectedFolder);
    }

    [Fact]
    public async Task CreateNewScriptCommand_SendsLaunchSkillCommandWithBishScripts()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var vm = new ScriptsPageViewModel(sender);

        await vm.CreateNewScriptCommand.ExecuteAsync(null);

        await sender.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-scripts"),
            Arg.Any<CancellationToken>());
    }
}

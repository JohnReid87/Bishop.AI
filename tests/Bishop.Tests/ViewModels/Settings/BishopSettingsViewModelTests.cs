using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Settings;

public class BishopSettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesOnlyMetaSkillsAsLaunchItems()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "Write a skill", [], "/bish-write-skill", Category: SkillCategory.Meta),
                new("bish-arch",        "Architecture review", [], "/bish-arch", Category: SkillCategory.Review),
                new("bish-audit-skills","Audit skills", [], "/bish-audit-skills", Category: SkillCategory.Meta),
            });

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>());
        await vm.LoadAsync();

        vm.MetaSkills.Should().HaveCount(2);
        vm.MetaSkills.Select(s => s.Name).Should().BeEquivalentTo(["bish-write-skill", "bish-audit-skills"]);
    }

    [Fact]
    public async Task LoadAsync_EmptyWhenNoMetaSkillsExist()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-arch", "Architecture review", [], "/bish-arch", Category: SkillCategory.Review),
            });

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>());
        await vm.LoadAsync();

        vm.MetaSkills.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_DerivesSlashCommandFromNameWhenCommandIsNull()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "", [], Command: null, Category: SkillCategory.Meta),
            });

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>());
        await vm.LoadAsync();

        vm.MetaSkills.Should().ContainSingle()
            .Which.RenderedCommand.Should().Be("/bish-write-skill");
    }

    [Fact]
    public async Task LoadAsync_RendersWorkspacePathPlaceholder()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "", [], Command: "claude --cd {{workspace_path}}", Category: SkillCategory.Meta),
            });

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>()) { WorkspacePath = @"C:\myrepo" };
        await vm.LoadAsync();

        vm.MetaSkills.Single().RenderedCommand.Should().Be(@"claude --cd C:\myrepo");
    }

    [Fact]
    public async Task LoadAsync_UsesSavedModelFromAppSettings()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "", [], "/bish-write-skill", Category: SkillCategory.Meta),
            });
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("skill.bish-write-skill.last_model", Arg.Any<CancellationToken>())
            .Returns("claude-opus-4-7");

        var vm = new BishopSettingsViewModel(mediator, appSettings);
        await vm.LoadAsync();

        vm.MetaSkills.Single().SavedModelId.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task LoadAsync_FallsBackToDefaultModelWhenAppSettingsEmpty()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "", [], "/bish-write-skill", Category: SkillCategory.Meta),
            });
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var vm = new BishopSettingsViewModel(mediator, appSettings);
        await vm.LoadAsync();

        vm.MetaSkills.Single().SavedModelId.Should().Be(SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public async Task LaunchAsync_SendsLaunchSkillCommandWithItemFields()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>()) { WorkspacePath = @"C:\repo" };

        var item = new SkillLaunchItem("bish-write-skill", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-write-skill", RequiresStage: false, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: null, new TerminalSnap(), "claude-opus-4-7");

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\repo" &&
                c.RenderedCommand == "/bish-write-skill" &&
                c.ModelId == "claude-opus-4-7"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_AppendsStagedTextToRenderedCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>()) { WorkspacePath = @"C:\repo" };

        var item = new SkillLaunchItem("bish-write-skill", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-write-skill", RequiresStage: true, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: "new-skill", new TerminalSnap(), "claude-sonnet-4-6");

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-write-skill new-skill"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_FallsBackToUserProfilePathWhenWorkspacePathUnset()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>());

        var item = new SkillLaunchItem("bish-write-skill", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-write-skill", RequiresStage: false, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: null, new TerminalSnap(), "claude-sonnet-4-6");

        var expectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.WorkspacePath == expectedPath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSkillModelAsync_WritesToAppSettings()
    {
        var mediator = Substitute.For<IMediator>();
        var appSettings = Substitute.For<IAppSettings>();
        var vm = new BishopSettingsViewModel(mediator, appSettings);

        await vm.SetSkillModelAsync("bish-arch", "claude-opus-4-7");

        await appSettings.Received(1).SetAsync("skill.bish-arch.last_model", "claude-opus-4-7", Arg.Any<CancellationToken>());
    }
}

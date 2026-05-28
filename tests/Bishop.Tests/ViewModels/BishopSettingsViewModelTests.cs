using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core.Skills;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class BishopSettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesOnlyMetaSkills()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-write-skill", "Write a skill", [], null, Category: SkillCategory.Meta),
                new("bish-arch",        "Architecture review", [], "/bish-arch", Category: SkillCategory.Review),
                new("bish-audit-skills","Audit skills", [], null, Category: SkillCategory.Meta),
            });

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync();

        vm.MetaSkills.Should().HaveCount(2)
            .And.OnlyContain(s => s.Category == SkillCategory.Meta);
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

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync();

        vm.MetaSkills.Should().BeEmpty();
    }

    [Fact]
    public async Task LaunchAsync_UsesWorkspacePathWhenSet()
    {
        var mediator = Substitute.For<IMediator>();
        var skill = new InstalledSkill("bish-write-skill", "", [], null, Category: SkillCategory.Meta);

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>()) { WorkspacePath = @"C:\myrepo" };
        await vm.LaunchAsync(skill);

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\myrepo" &&
                c.RenderedCommand == "/bish-write-skill"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_FallsBackToUserProfileWhenNoWorkspacePath()
    {
        var mediator = Substitute.For<IMediator>();
        var skill = new InstalledSkill("bish-write-skill", "", [], null, Category: SkillCategory.Meta);

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LaunchAsync(skill);

        var expectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.WorkspacePath == expectedPath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_PrefersCommandFieldOverDerivedSlashName()
    {
        var mediator = Substitute.For<IMediator>();
        var skill = new InstalledSkill("bish-write-skill", "", [], "claude /bish-write-skill", Category: SkillCategory.Meta);

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>()) { WorkspacePath = @"C:\myrepo" };
        await vm.LaunchAsync(skill);

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "claude /bish-write-skill"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_DerivesSlashCommandFromNameWhenCommandIsNull()
    {
        var mediator = Substitute.For<IMediator>();
        var skill = new InstalledSkill("bish-audit-skills", "", [], null, Category: SkillCategory.Meta);

        var vm = new BishopSettingsViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>()) { WorkspacePath = @"C:\myrepo" };
        await vm.LaunchAsync(skill);

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-audit-skills"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchCommandAsync_SendsLaunchSkillCommandWithSnapAndModel()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var vm = new BishopSettingsViewModel(mediator, Substitute.For<IAppSettings>());

        await vm.LaunchCommandAsync(@"C:\repo", "/bish-write-skill", new TerminalSnap(), "claude-opus-4-7");

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\repo" &&
                c.RenderedCommand == "/bish-write-skill" &&
                c.ModelId == "claude-opus-4-7"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSkillModelAsync_ReadsFromAppSettings()
    {
        var mediator = Substitute.For<IMediator>();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("skill.bish-arch.last_model", Arg.Any<CancellationToken>())
            .Returns("claude-sonnet-4-6");
        var vm = new BishopSettingsViewModel(mediator, appSettings);

        var result = await vm.GetSkillModelAsync("bish-arch");

        result.Should().Be("claude-sonnet-4-6");
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

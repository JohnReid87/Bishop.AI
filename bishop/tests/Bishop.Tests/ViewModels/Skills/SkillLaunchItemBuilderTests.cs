using Bishop.App.Services.Settings;
using Bishop.App.Skills;
using Bishop.Core.Skills;
using Bishop.ViewModels.Skills;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Skills;

public class SkillLaunchItemBuilderTests
{
    private static IAppSettings NoSavedModel()
    {
        var s = Substitute.For<IAppSettings>();
        s.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return s;
    }

    private static IAppSettings WithSavedModel(string skillName, string modelId)
    {
        var s = Substitute.For<IAppSettings>();
        s.GetAsync(SkillLaunchItemBuilder.LastModelKey(skillName), Arg.Any<CancellationToken>())
            .Returns(modelId);
        return s;
    }

    [Fact]
    public void LastModelKey_ReturnsExpectedFormat()
    {
        SkillLaunchItemBuilder.LastModelKey("bish-arch").Should().Be("skill.bish-arch.last_model");
    }

    [Fact]
    public async Task BuildAsync_WithMenuItemAndCard_RendersCardPlaceholders()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch {{card_number}}");
        var menuItem = new SkillMenuItem("bish-arch", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 42, cardTitle: "My card", cardDescription: "Desc",
            workspacePath: @"C:\repo", NoSavedModel());

        item.RenderedCommand.Should().Contain("42");
    }

    [Fact]
    public async Task BuildAsync_UsesSavedModelFromAppSettings()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch");
        var menuItem = new SkillMenuItem("bish-arch", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 1, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", WithSavedModel("bish-arch", "claude-opus-4-7"));

        item.SavedModelId.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task BuildAsync_FallsBackToDefaultModelWhenAppSettingsReturnsNull()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch");
        var menuItem = new SkillMenuItem("bish-arch", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 1, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", NoSavedModel());

        item.SavedModelId.Should().Be(SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public async Task BuildAsync_CommandNullOrEmpty_FallsBackToSlashName()
    {
        var skill = new InstalledSkill("bish-write-skill", "", ["workspace"], Command: null);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            skill, skill.Name, groupHeader: null,
            cardNumber: null, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", NoSavedModel());

        item.RenderedCommand.Should().Be("/bish-write-skill");
    }

    [Fact]
    public async Task BuildAsync_StagePrefillNull_ProducesNullStagePrefill()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch", StagePrefill: null);
        var menuItem = new SkillMenuItem("bish-arch", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 1, cardTitle: "T", cardDescription: "D",
            workspacePath: @"C:\repo", NoSavedModel());

        item.StagePrefill.Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_StagePrefillSet_RendersWithContext()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch",
            Stage: true, StagePrefill: "#{{card_number}}");
        var menuItem = new SkillMenuItem("bish-arch", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 7, cardTitle: "T", cardDescription: "D",
            workspacePath: @"C:\repo", NoSavedModel());

        item.StagePrefill.Should().Be("#7");
    }

    [Fact]
    public async Task BuildAsync_WithCard_SetsRequiresStageFromSkillStaging()
    {
        var skill = new InstalledSkill("bish-spec-cards", "", ["workspace"], "/bish-spec-cards", Stage: true);
        var menuItem = new SkillMenuItem("bish-spec-cards", skill);

        var withCard = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: 1, cardTitle: "T", cardDescription: "D",
            workspacePath: @"C:\repo", NoSavedModel());
        var withoutCard = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: null, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", NoSavedModel());

        withCard.RequiresStage.Should().BeFalse();
        withoutCard.RequiresStage.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_PropagatesGroupHeaderAndName()
    {
        var skill = new InstalledSkill("bish-arch", "", ["card"], "/bish-arch");
        var menuItem = new SkillMenuItem(Name: "Arch Review", skill, GroupHeader: "Code");

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: null, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", NoSavedModel());

        item.Name.Should().Be("Arch Review");
        item.GroupHeader.Should().Be("Code");
    }

    [Fact]
    public async Task BuildAsync_PropagatesStageProjectsAndStageFilePicker()
    {
        var skill = new InstalledSkill("bish-spec-cards", "", ["workspace"], "/bish-spec-cards",
            StageProjects: true, StageFilePicker: true);
        var menuItem = new SkillMenuItem("bish-spec-cards", skill);

        var item = await SkillLaunchItemBuilder.BuildAsync(
            menuItem, cardNumber: null, cardTitle: null, cardDescription: null,
            workspacePath: @"C:\repo", NoSavedModel());

        item.StageProjects.Should().BeTrue();
        item.StageFilePicker.Should().BeTrue();
    }
}

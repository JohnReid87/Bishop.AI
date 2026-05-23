using Bishop.App.Settings;
using Bishop.Core.Skills;
using Bishop.ViewModels;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class SkillViewerViewModelTests
{
    [Fact]
    public void Defaults_AreClosedAndEmpty()
    {
        var vm = NewVm();

        vm.IsOpen.Should().BeFalse();
        vm.Skill.Should().BeNull();
        vm.SkillName.Should().BeEmpty();
        vm.HasMetadata.Should().BeFalse();
        vm.ModelId.Should().Be(WorkNextOptionsDialogViewModel.DefaultModelId);
    }

    [Fact]
    public async Task OpenAsync_SetsSkillAndOpensPanel()
    {
        var skill = MakeSkill("bish-arch", scope: ["card"], command: "/bish-arch");
        var vm = NewVm();

        await vm.OpenAsync(skill);

        vm.IsOpen.Should().BeTrue();
        vm.Skill.Should().Be(skill);
        vm.SkillName.Should().Be("bish-arch");
        vm.ScopeText.Should().Be("scope: card");
        vm.CommandText.Should().Be("/bish-arch");
        vm.HasMetadata.Should().BeTrue();
    }

    [Fact]
    public void Close_HidesPanel()
    {
        var vm = NewVm();
        vm.IsOpen = true;

        vm.CloseCommand.Execute(null);

        vm.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void SetAutoPanelWidth_ClampsToBounds()
    {
        var vm = NewVm();

        vm.SetAutoPanelWidth(50);
        vm.PanelWidth.Should().Be(SkillViewerViewModel.MinPanelWidth);

        vm.SetAutoPanelWidth(99999);
        vm.PanelWidth.Should().Be(SkillViewerViewModel.MaxPanelWidth);
    }

    [Fact]
    public void ModelLabel_MapsModelIdToLabel()
    {
        var vm = NewVm();

        vm.ModelId = "claude-opus-4-7";
        vm.ModelLabel.Should().Be("Opus 4.7");

        vm.ModelId = "claude-unknown";
        vm.ModelLabel.Should().Be("Sonnet 4.6");
    }

    [Fact]
    public void ExtractBody_ReturnsContentAfterSecondFrontMatterDelimiter()
    {
        var content = "---\nname: x\n---\n# Body\nhello\n";

        SkillViewerViewModel.ExtractBody(content).Should().Be("# Body\nhello\n");
    }

    [Fact]
    public void ExtractBody_EmptyWhenNoFrontMatter()
    {
        SkillViewerViewModel.ExtractBody("just text").Should().BeEmpty();
    }

    private static SkillViewerViewModel NewVm() =>
        new(Substitute.For<IAppSettings>(), Path.Combine(Path.GetTempPath(), $"skill-viewer-test-{Guid.NewGuid():N}.json"));

    private static InstalledSkill MakeSkill(string name, IReadOnlyList<string>? scope = null, string? command = null) =>
        new(Name: name, Description: "desc", Scope: scope ?? [], Command: command);
}

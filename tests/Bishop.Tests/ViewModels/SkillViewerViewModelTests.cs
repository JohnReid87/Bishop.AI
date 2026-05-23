using Bishop.App.Settings;
using Bishop.Core.Skills;
using Bishop.ViewModels;
using FluentAssertions;
using NSubstitute;
using System.Text.Json;

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
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync(Arg.Any<string>()).Returns((string?)null);
        var vm = NewVm(appSettings);

        await vm.OpenAsync(skill);

        vm.IsOpen.Should().BeTrue();
        vm.Skill.Should().Be(skill);
        vm.SkillName.Should().Be("bish-arch");
        vm.ScopeText.Should().Be("scope: card");
        vm.CommandText.Should().Be("/bish-arch");
        vm.HasMetadata.Should().BeTrue();
        vm.ModelId.Should().Be(WorkNextOptionsDialogViewModel.DefaultModelId);
    }

    [Fact]
    public async Task OpenAsync_UsesPersistedModel_WhenSettingExists()
    {
        var skill = MakeSkill("bish-arch");
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.GetAsync("skill.bish-arch.last_model").Returns("claude-opus-4-7");
        var vm = NewVm(appSettings);

        await vm.OpenAsync(skill);

        vm.ModelId.Should().Be("claude-opus-4-7");
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

    // LoadAsync

    [Fact]
    public async Task LoadAsync_ResetsStateAndSetsWorkspaceId()
    {
        var vm = NewVm();
        vm.IsOpen = true;
        vm.Skill = MakeSkill("some-skill");
        vm.MarkdownBody = "old content";

        await vm.LoadAsync(Guid.NewGuid());

        vm.IsOpen.Should().BeFalse();
        vm.Skill.Should().BeNull();
        vm.MarkdownBody.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_RestoresPanelWidth_WhenPrefsFileExists()
    {
        var prefsPath = TempPath();
        var workspaceId = Guid.NewGuid();
        WritePrefs(prefsPath, workspaceId, panelWidth: 650);
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);

        await vm.LoadAsync(workspaceId);

        vm.PanelWidth.Should().Be(650);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultWidth_WhenNoPrefsFile()
    {
        var vm = NewVm();

        await vm.LoadAsync(Guid.NewGuid());

        vm.PanelWidth.Should().Be(SkillViewerViewModel.DefaultPanelWidth);
    }

    // SetModelAsync

    [Fact]
    public async Task SetModelAsync_UpdatesModelId()
    {
        var vm = NewVm();

        await vm.SetModelAsync("claude-opus-4-7");

        vm.ModelId.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task SetModelAsync_PersistsModel_WhenSkillIsSet()
    {
        var appSettings = Substitute.For<IAppSettings>();
        var vm = NewVm(appSettings);
        vm.Skill = MakeSkill("bish-arch");

        await vm.SetModelAsync("claude-opus-4-7");

        await appSettings.Received(1).SetAsync("skill.bish-arch.last_model", "claude-opus-4-7");
    }

    [Fact]
    public async Task SetModelAsync_DoesNotPersist_WhenSkillIsNull()
    {
        var appSettings = Substitute.For<IAppSettings>();
        var vm = NewVm(appSettings);

        await vm.SetModelAsync("claude-opus-4-7");

        await appSettings.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // RefreshAsync

    [Fact]
    public async Task RefreshCommand_DoesNothing_WhenSkillIsNull()
    {
        var vm = NewVm();
        vm.MarkdownBody = "existing";

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.MarkdownBody.Should().Be("existing");
    }

    [Fact]
    public async Task RefreshCommand_DoesNothing_WhenSourcePathDoesNotExist()
    {
        var vm = NewVm();
        vm.Skill = MakeSkill("bish-arch", sourcePath: @"C:\does\not\exist.md");

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.MarkdownBody.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshCommand_UpdatesMarkdownBody_WhenFileExists()
    {
        var path = TempPath(".md");
        await File.WriteAllTextAsync(path, "---\nname: x\n---\n# Content\n");
        var vm = NewVm();
        vm.Skill = MakeSkill("bish-arch", sourcePath: path);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.MarkdownBody.Should().Be("# Content\n");
    }

    // LoadPrefsAsync (exercised via LoadAsync)

    [Fact]
    public async Task LoadAsync_SwallowsException_WhenPrefsJsonIsInvalid()
    {
        var prefsPath = TempPath();
        await File.WriteAllTextAsync(prefsPath, "not valid json {{{");
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);

        var act = async () => await vm.LoadAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
        vm.PanelWidth.Should().Be(SkillViewerViewModel.DefaultPanelWidth);
    }

    [Fact]
    public async Task LoadAsync_KeepsDefaultWidth_WhenWorkspaceNotInPrefs()
    {
        var prefsPath = TempPath();
        WritePrefs(prefsPath, Guid.NewGuid(), panelWidth: 650); // different workspace
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);

        await vm.LoadAsync(Guid.NewGuid()); // different workspace ID

        vm.PanelWidth.Should().Be(SkillViewerViewModel.DefaultPanelWidth);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultWidth_WhenSavedWidthIsBelowMinimum()
    {
        var prefsPath = TempPath();
        var workspaceId = Guid.NewGuid();
        WritePrefs(prefsPath, workspaceId, panelWidth: 50);
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);

        await vm.LoadAsync(workspaceId);

        vm.PanelWidth.Should().Be(SkillViewerViewModel.DefaultPanelWidth);
    }

    // SavePrefsAsync (exercised via PanelWidth change)

    [Fact]
    public async Task SavePrefsAsync_WritesPrefsFile_AfterPanelWidthChange()
    {
        var prefsPath = TempPath();
        var workspaceId = Guid.NewGuid();
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);
        await vm.LoadAsync(workspaceId);

        vm.PanelWidth = 650;
        await Task.Delay(200);

        File.Exists(prefsPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(prefsPath);
        var all = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        all.Should().ContainKey(workspaceId.ToString());
        all[workspaceId.ToString()].GetProperty("PanelWidth").GetDouble().Should().Be(650);
    }

    [Fact]
    public async Task SavePrefsAsync_DoesNotWriteFile_WhenWorkspaceIdIsEmpty()
    {
        var prefsPath = TempPath();
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);
        // No LoadAsync — _workspaceId stays Guid.Empty

        vm.PanelWidth = 650;
        await Task.Delay(100);

        File.Exists(prefsPath).Should().BeFalse();
    }

    [Fact]
    public async Task SavePrefsAsync_MergesWithExistingEntries()
    {
        var prefsPath = TempPath();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        WritePrefs(prefsPath, idB, panelWidth: 700);

        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);
        await vm.LoadAsync(idA);

        vm.PanelWidth = 650;
        await Task.Delay(200);

        var json = await File.ReadAllTextAsync(prefsPath);
        var all = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        all.Should().ContainKey(idA.ToString());
        all.Should().ContainKey(idB.ToString());
    }

    [Fact]
    public async Task SavePrefsAsync_SwallowsException_WhenWriteFails()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bsvt-{Guid.NewGuid():N}");
        var prefsPath = Path.Combine(dir, "prefs.json");
        Directory.CreateDirectory(prefsPath); // "prefs.json" as a directory causes write failure

        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);
        await vm.LoadAsync(Guid.NewGuid());

        var act = async () =>
        {
            vm.PanelWidth = 650;
            await Task.Delay(200);
        };

        await act.Should().NotThrowAsync();

        Directory.Delete(dir, true);
    }

    // OnPanelWidthChanged suppression

    [Fact]
    public async Task OnPanelWidthChanged_DoesNotSave_WhileLoadingPrefs()
    {
        var prefsPath = TempPath();
        var workspaceId = Guid.NewGuid();
        WritePrefs(prefsPath, workspaceId, panelWidth: 650);
        var vm = new SkillViewerViewModel(Substitute.For<IAppSettings>(), prefsPath);

        var mtimeBefore = File.GetLastWriteTimeUtc(prefsPath);
        await vm.LoadAsync(workspaceId);
        await Task.Delay(200); // allow any incorrectly triggered SavePrefsAsync to complete

        File.GetLastWriteTimeUtc(prefsPath).Should().Be(mtimeBefore);
        vm.PanelWidth.Should().Be(650);
    }

    // ExtractBody edge cases

    [Fact]
    public void ExtractBody_EmptyWhenOnlyOpeningDelimiter()
    {
        SkillViewerViewModel.ExtractBody("---\nname: x\n").Should().BeEmpty();
    }

    [Fact]
    public void ExtractBody_EmptyWhenBodyIsEmptyAfterClosingDelimiter()
    {
        SkillViewerViewModel.ExtractBody("---\nname: x\n---\n").Should().BeEmpty();
    }

    [Fact]
    public void ExtractBody_EmptyWhenClosingDelimiterHasNoTrailingContent()
    {
        SkillViewerViewModel.ExtractBody("---\nname: x\n---").Should().BeEmpty();
    }

    [Fact]
    public void ExtractBody_HandlesCarriageReturnLineFeedLineEndings()
    {
        var crlfContent = "---\r\nname: x\r\n---\r\n# Body\r\nhello\r\n";

        SkillViewerViewModel.ExtractBody(crlfContent).Should().Be("# Body\nhello\n");
    }

    // Helpers

    private static SkillViewerViewModel NewVm(IAppSettings? appSettings = null) =>
        new(appSettings ?? Substitute.For<IAppSettings>(), TempPath());

    private static string TempPath(string ext = ".json") =>
        Path.Combine(Path.GetTempPath(), $"skill-viewer-test-{Guid.NewGuid():N}{ext}");

    private static InstalledSkill MakeSkill(
        string name,
        IReadOnlyList<string>? scope = null,
        string? command = null,
        string sourcePath = "") =>
        new(Name: name, Description: "desc", Scope: scope ?? [], Command: command, SourcePath: sourcePath);

    private static void WritePrefs(string path, Guid workspaceId, double panelWidth)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var all = new Dictionary<string, object> { [workspaceId.ToString()] = new { PanelWidth = panelWidth } };
        File.WriteAllText(path, JsonSerializer.Serialize(all));
    }
}

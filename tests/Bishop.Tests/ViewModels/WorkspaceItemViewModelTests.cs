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

namespace Bishop.Tests.ViewModels;

public class WorkspaceItemViewModelTests
{
    [Fact]
    public void FirstLetter_UppercaseInitial()
    {
        new WorkspaceItemViewModel { Name = "bishop" }.FirstLetter.Should().Be("B");
        new WorkspaceItemViewModel { Name = "Acme" }.FirstLetter.Should().Be("A");
    }

    [Fact]
    public void FirstLetter_QuestionMarkWhenNameEmpty()
    {
        new WorkspaceItemViewModel { Name = string.Empty }.FirstLetter.Should().Be("?");
    }

    [Fact]
    public void Path_SettingMissingDirectoryMarksPathMissing()
    {
        var vm = new WorkspaceItemViewModel();

        vm.Path = @"C:\definitely\not\a\real\directory\__xyz__";

        vm.IsPathMissing.Should().BeTrue();
    }

    [Fact]
    public void Path_SettingExistingDirectoryClearsPathMissing()
    {
        var vm = new WorkspaceItemViewModel { IsPathMissing = true };
        var existing = Path.GetTempPath();

        vm.Path = existing;

        vm.IsPathMissing.Should().BeFalse();
    }
}

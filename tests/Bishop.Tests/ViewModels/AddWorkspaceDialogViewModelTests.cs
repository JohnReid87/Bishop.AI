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

public class AddWorkspaceDialogViewModelTests
{
    [Fact]
    public void CanConfirm_FalseWhenNameOrPathBlank()
    {
        var vm = new AddWorkspaceDialogViewModel();

        vm.CanConfirm.Should().BeFalse();

        vm.Name = "Project";
        vm.CanConfirm.Should().BeFalse();

        vm.FolderPath = @"C:\code\project";
        vm.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void CanConfirm_FalseWhenWhitespaceOnly()
    {
        var vm = new AddWorkspaceDialogViewModel { Name = "   ", FolderPath = "   " };

        vm.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void IsPickExisting_DefaultsToTrue()
    {
        var vm = new AddWorkspaceDialogViewModel();

        vm.IsPickExisting.Should().BeTrue();
    }
}

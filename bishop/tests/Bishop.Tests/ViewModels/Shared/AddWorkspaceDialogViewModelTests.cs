using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Shared;

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

    [Fact]
    public void CreateMode_CanConfirmRequiresParentAndNewFolderName()
    {
        var vm = new AddWorkspaceDialogViewModel(_ => false)
        {
            IsPickExisting = false,
            Name = "Project",
        };

        vm.CanConfirm.Should().BeFalse();

        vm.ParentFolderPath = @"C:\code";
        vm.CanConfirm.Should().BeFalse();

        vm.NewFolderName = "project";
        vm.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void CreateMode_EffectivePathCombinesParentAndName()
    {
        var vm = new AddWorkspaceDialogViewModel(_ => false)
        {
            IsPickExisting = false,
            ParentFolderPath = @"C:\code",
            NewFolderName = "project",
        };

        vm.EffectivePath.Should().Be(System.IO.Path.Combine(@"C:\code", "project"));
    }

    [Fact]
    public void CreateMode_CollisionDisablesConfirmAndExposesError()
    {
        var existing = System.IO.Path.Combine(@"C:\code", "project");
        var vm = new AddWorkspaceDialogViewModel(p => string.Equals(p, existing, System.StringComparison.OrdinalIgnoreCase))
        {
            IsPickExisting = false,
            Name = "Project",
            ParentFolderPath = @"C:\code",
            NewFolderName = "project",
        };

        vm.HasCollision.Should().BeTrue();
        vm.CollisionError.Should().NotBeNullOrEmpty();
        vm.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void CreateMode_NoCollisionWhenParentOrNameBlank()
    {
        var vm = new AddWorkspaceDialogViewModel(_ => true)
        {
            IsPickExisting = false,
            Name = "Project",
        };

        vm.HasCollision.Should().BeFalse();
        vm.CollisionError.Should().BeNull();
    }

    [Fact]
    public void PickExistingMode_IgnoresCollisionProbe()
    {
        var vm = new AddWorkspaceDialogViewModel(_ => true)
        {
            IsPickExisting = true,
            Name = "Project",
            FolderPath = @"C:\code\project",
            ParentFolderPath = @"C:\code",
            NewFolderName = "project",
        };

        vm.HasCollision.Should().BeFalse();
        vm.CanConfirm.Should().BeTrue();
    }
}

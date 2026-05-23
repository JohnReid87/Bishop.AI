using Bishop.App.Services.CatMode;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void IsWorkspaceListEmpty_TrueOnConstruction()
    {
        var vm = NewVm();

        vm.IsWorkspaceListEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsContentEmpty_TrueWhenNoSelection()
    {
        var vm = NewVm();

        vm.IsContentEmpty.Should().BeTrue();

        vm.SelectedWorkspace = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };
        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsCatModeActive_ReflectsCatModeService()
    {
        var catMode = new CatModeService();
        var vm = NewVm(catMode: catMode);

        vm.IsCatModeActive.Should().BeFalse();

        catMode.Toggle();

        vm.IsCatModeActive.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_PopulatesWorkspacesFromMediator()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = id1, Name = "alpha", Path = "C:/a", Position = 1, GitHubRepo = "owner/repo" },
                new() { Id = id2, Name = "beta", Path = "C:/b", Position = 2, GitHubRepo = null },
            });

        var vm = NewVm(mediator: mediator);

        await vm.LoadAsync();

        vm.Workspaces.Should().HaveCount(2);
        vm.IsWorkspaceListEmpty.Should().BeFalse();

        var first = vm.Workspaces[0];
        first.Id.Should().Be(id1);
        first.Name.Should().Be("alpha");
        first.Path.Should().Be("C:/a");
        first.Position.Should().Be(1);
        first.GitHubRepo.Should().Be("owner/repo");

        var second = vm.Workspaces[1];
        second.Id.Should().Be(id2);
        second.Name.Should().Be("beta");
        second.Path.Should().Be("C:/b");
        second.Position.Should().Be(2);
        second.GitHubRepo.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_LeavesSelectionNull_WhenNoNavPrefsFile()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
            });
        var vm = NewVm(mediator: mediator);

        await vm.LoadAsync();

        vm.SelectedWorkspace.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_RestoresSelectedWorkspace_WhenNavPrefsHasMatchingId()
    {
        var targetId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "other", Path = "C:/other", Position = 1 },
                new() { Id = targetId, Name = "target", Path = "C:/target", Position = 2 },
            });

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath,
                $"{{\"IsPaneOpen\":false,\"LastSelectedWorkspaceId\":\"{targetId}\"}}");

            var vm = NewVm(mediator: mediator, navPrefsFilePath: tempPath);
            await vm.LoadAsync();

            vm.SelectedWorkspace.Should().NotBeNull();
            vm.SelectedWorkspace!.Id.Should().Be(targetId);
            vm.IsPaneOpen.Should().BeFalse();
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* SaveNavPrefsAsync may still hold the file */ }
        }
    }

    [Fact]
    public async Task LoadAsync_LeavesSelectionNull_WhenNavPrefsReferenceUnknownId()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
            });

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath,
                $"{{\"IsPaneOpen\":true,\"LastSelectedWorkspaceId\":\"{Guid.NewGuid()}\"}}");

            var vm = NewVm(mediator: mediator, navPrefsFilePath: tempPath);
            await vm.LoadAsync();

            vm.SelectedWorkspace.Should().BeNull();
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* SaveNavPrefsAsync may still hold the file */ }
        }
    }

    [Fact]
    public async Task LoadAsync_StillLoadsWorkspaces_WhenNavPrefsFileContainsInvalidJson()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
            });

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, "this is not valid json {{{");

            var vm = NewVm(mediator: mediator, navPrefsFilePath: tempPath);
            await vm.LoadAsync();

            vm.Workspaces.Should().HaveCount(1);
            vm.SelectedWorkspace.Should().BeNull();
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* SaveNavPrefsAsync may still hold the file */ }
        }
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_SetsIsPathMissing_WhenPathDoesNotExist()
    {
        var vm = NewVm();
        var item = new WorkspaceItemViewModel
        {
            Id = Guid.NewGuid(),
            Name = "ws",
            Path = @"C:\definitely\not\a\real\path\__xyz__bishop__",
        };

        vm.SelectedWorkspace = item;

        item.IsPathMissing.Should().BeTrue();
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_MarksSingleItemSelected()
    {
        var vm = NewVm();
        var item1 = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "a" };
        var item2 = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "b" };
        vm.Workspaces.Add(item1);
        vm.Workspaces.Add(item2);

        vm.SelectedWorkspace = item1;

        item1.IsSelected.Should().BeTrue();
        item2.IsSelected.Should().BeFalse();

        vm.SelectedWorkspace = item2;

        item1.IsSelected.Should().BeFalse();
        item2.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ToggleCatModeCommand_CallsToggle()
    {
        var catMode = Substitute.For<ICatModeService>();
        var vm = NewVm(catMode: catMode);

        vm.ToggleCatModeCommand.Execute(null);

        catMode.Received(1).Toggle();
    }

    [Fact]
    public async Task AddWorkspaceAsync_SendsCreateWorkspaceCommand_WithDialogValues()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CreateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Workspace { Id = Guid.NewGuid(), Name = "New", Path = @"C:\new", Position = 1 });
        var vm = NewVm(mediator: mediator);
        var dialog = new AddWorkspaceDialogViewModel
        {
            Name = "New",
            FolderPath = @"C:\new",
            IsPickExisting = false,
        };

        await vm.AddWorkspaceAsync(dialog);

        await mediator.Received(1).Send(
            Arg.Is<CreateWorkspaceCommand>(c => c.Name == "New" && c.Path == @"C:\new" && c.InitGit == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddWorkspaceAsync_AddsWorkspaceToCollectionAndSelectsIt()
    {
        var newId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CreateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Workspace { Id = newId, Name = "New", Path = @"C:\new", Position = 1 });
        var vm = NewVm(mediator: mediator);
        var dialog = new AddWorkspaceDialogViewModel { Name = "New", FolderPath = @"C:\new" };

        await vm.AddWorkspaceAsync(dialog);

        vm.Workspaces.Should().HaveCount(1);
        vm.Workspaces[0].Id.Should().Be(newId);
        vm.SelectedWorkspace.Should().BeSameAs(vm.Workspaces[0]);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_SendsDeleteWorkspaceCommand()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = id, Name = "ws" };

        await vm.DeleteWorkspaceAsync(item);

        await mediator.Received(1).Send(
            Arg.Is<DeleteWorkspaceCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_RemovesItemFromCollection()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };
        vm.Workspaces.Add(item);

        await vm.DeleteWorkspaceAsync(item);

        vm.Workspaces.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ClearsSelection_WhenDeletedItemWasSelected()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };
        vm.Workspaces.Add(item);
        vm.SelectedWorkspace = item;

        await vm.DeleteWorkspaceAsync(item);

        vm.SelectedWorkspace.Should().BeNull();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_PreservesSelection_WhenDeletedItemWasNotSelected()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var selected = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "selected" };
        var other = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "other" };
        vm.Workspaces.Add(selected);
        vm.Workspaces.Add(other);
        vm.SelectedWorkspace = selected;

        await vm.DeleteWorkspaceAsync(other);

        vm.SelectedWorkspace.Should().BeSameAs(selected);
    }

    [Fact]
    public async Task RenameWorkspaceAsync_SendsUpdateWorkspaceCommand_WithCurrentNameAndPath()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Workspace { Id = id, Name = "renamed", Path = @"C:\ws", Position = 1 });
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = id, Name = "renamed", Path = @"C:\ws" };

        await vm.RenameWorkspaceAsync(item);

        await mediator.Received(1).Send(
            Arg.Is<UpdateWorkspaceCommand>(c => c.Id == id && c.Name == "renamed" && c.Path == @"C:\ws"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepathWorkspaceAsync_SendsUpdateCommandAndUpdatesItemPath()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Workspace { Id = id, Name = "ws", Path = @"C:\new-path", Position = 1 });
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = id, Name = "ws", Path = @"C:\old-path" };

        await vm.RepathWorkspaceAsync(item, @"C:\new-path");

        await mediator.Received(1).Send(
            Arg.Is<UpdateWorkspaceCommand>(c => c.Id == id && c.Path == @"C:\new-path"),
            Arg.Any<CancellationToken>());
        item.Path.Should().Be(@"C:\new-path");
    }

    [Fact]
    public async Task PersistReorderAsync_SendsReorderWorkspacesCommand_WithOrderedIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReorderWorkspacesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var items = new[]
        {
            new WorkspaceItemViewModel { Id = id1, Name = "alpha" },
            new WorkspaceItemViewModel { Id = id2, Name = "beta" },
        };

        await vm.PersistReorderAsync(items);

        await mediator.Received(1).Send(
            Arg.Is<ReorderWorkspacesCommand>(c => c.OrderedIds.SequenceEqual(new[] { id1, id2 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistReorderAsync_AssignsSequentialPositionsStartingAtOne()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReorderWorkspacesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var items = new[]
        {
            new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "alpha" },
            new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "beta" },
            new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "gamma" },
        };

        await vm.PersistReorderAsync(items);

        items[0].Position.Should().Be(1);
        items[1].Position.Should().Be(2);
        items[2].Position.Should().Be(3);
    }

    private static MainWindowViewModel NewVm(
        IMediator? mediator = null,
        ICatModeService? catMode = null,
        string? navPrefsFilePath = null) =>
        new(
            mediator ?? Substitute.For<IMediator>(),
            catMode ?? new CatModeService(),
            navPrefsFilePath ?? Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));
}

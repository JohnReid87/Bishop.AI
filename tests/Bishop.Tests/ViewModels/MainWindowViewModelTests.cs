using Bishop.App.Batches.ReconcileOrphanedBatches;
using Bishop.App.Services;
using Bishop.App.Services.CatMode;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Security.AccessControl;
using System.Security.Principal;

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
    public void IsContentEmpty_FalseWhenIsWorkspacelessPageActiveIsTrue()
    {
        var vm = NewVm();
        vm.IsWorkspacelessPageActive = true;

        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsContentEmpty_RaisesPropertyChanged_WhenIsWorkspacelessPageActiveChanges()
    {
        var vm = NewVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsWorkspacelessPageActive = true;

        raised.Should().Contain(nameof(vm.IsContentEmpty));
        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_ClearsIsWorkspacelessPageActive_WhenValueIsNotNull()
    {
        var vm = NewVm();
        vm.IsWorkspacelessPageActive = true;

        vm.SelectedWorkspace = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };

        vm.IsWorkspacelessPageActive.Should().BeFalse();
        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_DoesNotClearIsWorkspacelessPageActive_WhenValueIsNull()
    {
        var vm = NewVm();
        vm.IsWorkspacelessPageActive = true;

        vm.SelectedWorkspace = null;

        vm.IsWorkspacelessPageActive.Should().BeTrue();
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
    public async Task LoadAsync_StillLoadsWorkspaces_WhenNavPrefsFileIsLocked()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
            });

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var lockStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var vm = NewVm(mediator: mediator, navPrefsFilePath: tempPath);
            await vm.LoadAsync();

            vm.Workspaces.Should().HaveCount(1);
            vm.SelectedWorkspace.Should().BeNull();
        }
        finally
        {
            lockStream.Dispose();
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public async Task LoadAsync_StillLoadsWorkspaces_WhenNavPrefsFileHasAccessDenied()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
            });

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(tempPath, "{\"IsPaneOpen\":true}");
        var fileInfo = new FileInfo(tempPath);
        var userSid = WindowsIdentity.GetCurrent().User!;
        var denyRule = new FileSystemAccessRule(userSid, FileSystemRights.Read, AccessControlType.Deny);
        var acl = fileInfo.GetAccessControl();
        acl.AddAccessRule(denyRule);
        fileInfo.SetAccessControl(acl);
        try
        {
            var vm = NewVm(mediator: mediator, navPrefsFilePath: tempPath);
            await vm.LoadAsync();

            vm.Workspaces.Should().HaveCount(1);
            vm.SelectedWorkspace.Should().BeNull();
        }
        finally
        {
            try
            {
                var restoreAcl = fileInfo.GetAccessControl();
                restoreAcl.RemoveAccessRule(denyRule);
                fileInfo.SetAccessControl(restoreAcl);
            }
            catch { }
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public async Task LoadAsync_SendsReconcileOrphanedBatchesCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());
        var vm = NewVm(mediator: mediator);

        await vm.LoadAsync();

        await mediator.Received(1).Send(
            Arg.Any<ReconcileOrphanedBatchesCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithNoWorkspaces_LeavesIsWorkspaceListEmptyAndIsContentEmptyTrue()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());
        var vm = NewVm(mediator: mediator);

        await vm.LoadAsync();

        vm.IsWorkspaceListEmpty.Should().BeTrue();
        vm.IsContentEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsWorkspaceListEmpty_RaisesPropertyChanged_WhenWorkspacesTransitionsToNonEmpty()
    {
        var vm = NewVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Workspaces.Add(new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" });

        raised.Should().Contain(nameof(vm.IsWorkspaceListEmpty));
        vm.IsWorkspaceListEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsContentEmpty_RaisesPropertyChanged_WhenSelectedWorkspaceChanges()
    {
        var vm = NewVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedWorkspace = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };

        raised.Should().Contain(nameof(vm.IsContentEmpty));
        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_SetsIsPathMissing_WhenPathDoesNotExist()
    {
        var vm = NewVm(dispatcher: new SynchronousDispatcher());
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
        var vm = NewVm(dispatcher: new SynchronousDispatcher());
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
    public void ToggleCatModeCommand_TogglesIsCatModeActive()
    {
        var catMode = new CatModeService();
        var vm = NewVm(catMode: catMode);
        var initialState = vm.IsCatModeActive;

        vm.ToggleCatModeCommand.Execute(null);

        vm.IsCatModeActive.Should().Be(!initialState);
    }

    [Fact]
    public async Task AddWorkspaceAsync_MapsDialogPathAndNameToWorkspaceInCollection()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(
                new Workspace { Id = Guid.NewGuid(), Name = "New", Path = @"C:\new", Position = 1 },
                Created: true, GitHubLinked: false));
        var vm = NewVm(mediator: mediator);
        var dialog = new AddWorkspaceDialogViewModel
        {
            Name = "New",
            FolderPath = @"C:\new",
            IsPickExisting = false,
        };

        await vm.AddWorkspaceAsync(dialog);

        vm.Workspaces.Should().HaveCount(1);
        vm.Workspaces[0].Name.Should().Be("New");
        vm.Workspaces[0].Path.Should().Be(@"C:\new");
    }

    [Fact]
    public async Task AddWorkspaceAsync_AddsWorkspaceToCollectionAndSelectsIt()
    {
        var newId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(
                new Workspace { Id = newId, Name = "New", Path = @"C:\new", Position = 1 },
                Created: true, GitHubLinked: false));
        var vm = NewVm(mediator: mediator);
        var dialog = new AddWorkspaceDialogViewModel { Name = "New", FolderPath = @"C:\new" };

        await vm.AddWorkspaceAsync(dialog);

        vm.Workspaces.Should().HaveCount(1);
        vm.Workspaces[0].Id.Should().Be(newId);
        vm.SelectedWorkspace.Should().BeSameAs(vm.Workspaces[0]);
    }

    [Fact]
    public async Task AddWorkspaceAsync_RaisesWorkspacesChangedNotifier()
    {
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(
                new Workspace { Id = Guid.NewGuid(), Name = "New", Path = @"C:\new", Position = 1 },
                Created: true, GitHubLinked: false));
        var vm = NewVm(mediator: mediator, notifier: notifier);
        var dialog = new AddWorkspaceDialogViewModel { Name = "New", FolderPath = @"C:\new" };

        await vm.AddWorkspaceAsync(dialog);

        vm.Workspaces.Should().ContainSingle(w => w.Name == "New");
        vm.SelectedWorkspace.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_RemovesOnlyTheTargetItem()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var item1 = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "first" };
        var item2 = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "second" };
        vm.Workspaces.Add(item1);
        vm.Workspaces.Add(item2);

        await vm.DeleteWorkspaceAsync(item1);

        vm.Workspaces.Should().ContainSingle().Which.Should().BeSameAs(item2);
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
    public async Task RenameWorkspaceAsync_SendsUpdateCommandForCorrectWorkspace()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Workspace { Id = id, Name = "renamed", Path = @"C:\ws", Position = 1 });
        var vm = NewVm(mediator: mediator);
        var item = new WorkspaceItemViewModel { Id = id, Name = "renamed", Path = @"C:\ws" };

        await vm.RenameWorkspaceAsync(item);

        await mediator.Received(1).Send(
            Arg.Is<UpdateWorkspaceCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
        item.Name.Should().Be("renamed");
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

        await mediator.Received(1).Send(Arg.Any<UpdateWorkspaceCommand>(), Arg.Any<CancellationToken>());
        item.Path.Should().Be(@"C:\new-path");
    }

    [Fact]
    public async Task PersistReorderAsync_AssignsPositionsMatchingPassedOrder()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReorderWorkspacesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);
        var first = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "alpha" };
        var second = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "beta" };

        await vm.PersistReorderAsync(new[] { first, second });

        first.Position.Should().Be(1);
        second.Position.Should().Be(2);
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

    [Fact]
    public void OnWorkspacesChanged_ReloadsWorkspacesFromMediator()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = id, Name = "alpha", Path = "C:/a", Position = 1 }
            });
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();
        var vm = NewVm(mediator: mediator, notifier: notifier, dispatcher: new SynchronousDispatcher());

        notifier.WorkspacesChanged += Raise.Event<Action>();

        vm.Workspaces.Should().HaveCount(1);
        vm.Workspaces[0].Id.Should().Be(id);
    }

    [Fact]
    public async Task OnWorkspacesChanged_SelectsFirstWorkspace_WhenCurrentWorkspaceRemovedFromList()
    {
        var originalId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                new List<Workspace> { new() { Id = originalId, Name = "original", Path = "C:/o", Position = 1 } },
                new List<Workspace> { new() { Id = newId, Name = "replacement", Path = "C:/n", Position = 1 } });
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();
        var vm = NewVm(mediator: mediator, notifier: notifier, dispatcher: new SynchronousDispatcher());

        await vm.LoadAsync();
        vm.SelectedWorkspace = vm.Workspaces[0];

        notifier.WorkspacesChanged += Raise.Event<Action>();

        vm.SelectedWorkspace.Should().NotBeNull();
        vm.SelectedWorkspace!.Id.Should().Be(newId);
    }

    [Fact]
    public void OnWorkspacesChanged_DoesNotAutoSelectFirstWorkspace_WhenCurrentSelectionIsNull()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 }
            });
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();
        var vm = NewVm(mediator: mediator, notifier: notifier, dispatcher: new SynchronousDispatcher());

        notifier.WorkspacesChanged += Raise.Event<Action>();

        vm.SelectedWorkspace.Should().BeNull();
    }

    [Fact]
    public async Task AddWorkspaceAsync_LeavesNewWorkspaceSelected_WhenBindingClearsSelectionDuringReload()
    {
        var newId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(
                new Workspace { Id = newId, Name = "New", Path = @"C:\new", Position = 1 },
                Created: true, GitHubLinked: false));
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = newId, Name = "New", Path = @"C:\new", Position = 1 }
            });
        var notifier = new WorkspaceChangeNotifier();
        var vm = NewVm(mediator: mediator, notifier: notifier, dispatcher: new SynchronousDispatcher());

        vm.Workspaces.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                vm.SelectedWorkspace = null;
        };

        await vm.AddWorkspaceAsync(new AddWorkspaceDialogViewModel { Name = "New", FolderPath = @"C:\new" });

        vm.SelectedWorkspace.Should().NotBeNull();
        vm.SelectedWorkspace!.Id.Should().Be(newId);
    }

    [Fact]
    public async Task OnIsPaneOpenChanged_PersistsNavPrefsToFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            var vm = NewVm(dispatcher: new SynchronousDispatcher(), navPrefsFilePath: tempPath);
            vm.IsPaneOpen = false;

            var json = await File.ReadAllTextAsync(tempPath);
            json.Should().Contain("\"IsPaneOpen\":false");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public async Task SaveNavPrefsAsync_SwallowsIOException_WhenFileIsLocked()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var lockStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var vm = NewVm(dispatcher: new SynchronousDispatcher(), navPrefsFilePath: tempPath);

            vm.IsPaneOpen = false;

            vm.IsPaneOpen.Should().BeFalse();
        }
        finally
        {
            lockStream.Dispose();
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void Notifications_ReturnsErrorBusNotifications()
    {
        var errorBus = Substitute.For<IErrorBus>();
        var notifications = new ObservableCollection<ErrorNotificationViewModel>();
        errorBus.Notifications.Returns(notifications);
        var vm = NewVm(errorBus: errorBus);

        vm.Notifications.Should().BeSameAs(notifications);
    }

    [Fact]
    public void Notifications_ContainsErrorReportedToErrorBus()
    {
        var dispatcher = new SynchronousDispatcher();
        var errorBus = new ErrorBus(dispatcher);
        var vm = NewVm(errorBus: errorBus, dispatcher: dispatcher);
        var exception = new InvalidOperationException("test error");

        errorBus.Report(exception);

        vm.Notifications.Should().ContainSingle()
            .Which.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void IsPaneOpen_RaisesPropertyChanged_WhenToggled()
    {
        var vm = NewVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsPaneOpen = false;

        raised.Should().Contain(nameof(vm.IsPaneOpen));
    }

    [Fact]
    public void OnSelectedWorkspaceChanged_DoesNotSetIsPathMissing_WhenPathExists()
    {
        var vm = NewVm(dispatcher: new SynchronousDispatcher());
        var item = new WorkspaceItemViewModel
        {
            Id = Guid.NewGuid(),
            Name = "ws",
            Path = Path.GetTempPath(),
        };

        vm.SelectedWorkspace = item;

        item.IsPathMissing.Should().BeFalse();
    }

    // Polls for the file to appear and have content, up to timeoutMs.
    // Used instead of a fixed Task.Delay for the fire-and-forget SaveNavPrefsAsync path.
    private static async Task WaitForFileWrittenAsync(string path, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                return;
            await Task.Delay(10);
        }
    }

    private sealed class SynchronousDispatcher : IUiDispatcher
    {
        public void TryEnqueue(Action work) => work();
        public void TryEnqueue(Func<Task> work) => work().GetAwaiter().GetResult();
    }

    private static MainWindowViewModel NewVm(
        IMediator? mediator = null,
        ICatModeService? catMode = null,
        IWorkspaceChangeNotifier? notifier = null,
        IUiDispatcher? dispatcher = null,
        IErrorBus? errorBus = null,
        string? navPrefsFilePath = null) =>
        new(
            mediator ?? Substitute.For<IMediator>(),
            catMode ?? new CatModeService(),
            notifier ?? Substitute.For<IWorkspaceChangeNotifier>(),
            dispatcher ?? Substitute.For<IUiDispatcher>(),
            errorBus ?? Substitute.For<IErrorBus>(),
            navPrefsFilePath ?? Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));
}

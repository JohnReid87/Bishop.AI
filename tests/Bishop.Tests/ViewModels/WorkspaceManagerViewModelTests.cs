using Bishop.App.Services;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.App.Workspaces.RemoveWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class WorkspaceManagerViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesWorkspacesIncludingRemoved()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "active-ws", Path = @"C:\active", IsRemoved = false },
                new() { Id = Guid.NewGuid(), Name = "removed-ws", Path = @"C:\removed", IsRemoved = true, RemovedAt = DateTimeOffset.UtcNow },
            });

        var vm = new WorkspaceManagerViewModel(mediator, Substitute.For<IWorkspaceChangeNotifier>());
        await vm.LoadAsync();

        vm.Workspaces.Should().HaveCount(2);
        vm.Workspaces.Should().ContainSingle(w => w.Name == "active-ws" && !w.IsRemoved);
        vm.Workspaces.Should().ContainSingle(w => w.Name == "removed-ws" && w.IsRemoved);
    }

    [Fact]
    public async Task LoadAsync_SendsQueryWithIncludeRemovedTrue()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());

        var vm = new WorkspaceManagerViewModel(mediator, Substitute.For<IWorkspaceChangeNotifier>());
        await vm.LoadAsync();

        await mediator.Received(1).Send(
            Arg.Is<ListWorkspacesQuery>(q => q.IncludeRemoved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_SendsRemoveCommandThenReloads()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());

        var vm = new WorkspaceManagerViewModel(mediator, Substitute.For<IWorkspaceChangeNotifier>());
        await vm.RemoveAsync(id);

        await mediator.Received(1).Send(
            Arg.Is<RemoveWorkspaceCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
        await mediator.Received().Send(
            Arg.Any<ListWorkspacesQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeAsync_SendsPurgeCommandThenReloads()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());

        var vm = new WorkspaceManagerViewModel(mediator, Substitute.For<IWorkspaceChangeNotifier>());
        await vm.PurgeAsync(id);

        await mediator.Received(1).Send(
            Arg.Is<PurgeWorkspaceCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
        await mediator.Received().Send(
            Arg.Any<ListWorkspacesQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_RaisesWorkspacesChangedNotifier()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();

        var vm = new WorkspaceManagerViewModel(mediator, notifier);
        await vm.RemoveAsync(id);

        notifier.Received(1).NotifyChanged();
    }

    [Fact]
    public async Task PurgeAsync_RaisesWorkspacesChangedNotifier()
    {
        var id = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>());
        var notifier = Substitute.For<IWorkspaceChangeNotifier>();

        var vm = new WorkspaceManagerViewModel(mediator, notifier);
        await vm.PurgeAsync(id);

        notifier.Received(1).NotifyChanged();
    }

    [Fact]
    public void StatusText_ReturnsActiveForNonRemovedWorkspace()
    {
        var item = new WorkspaceManagerItemViewModel { IsRemoved = false };
        item.StatusText.Should().Be("active");
    }

    [Fact]
    public void StatusText_ReturnsRemovedDateForRemovedWorkspace()
    {
        var removedAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var item = new WorkspaceManagerItemViewModel { IsRemoved = true, RemovedAt = removedAt };
        item.StatusText.Should().Be("removed 2026-03-15");
    }
}

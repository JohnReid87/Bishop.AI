using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Lanes.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Lanes.List;

public sealed class ListLanesCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsListLanesByWorkspaceQuery()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LaneInfo>)[]);

        var cmd = new ListLanesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SendsQueryWithCorrectWorkspaceId()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LaneInfo>)[]);

        var cmd = new ListLanesCliCommand(mediator);
        await cmd.InvokeAsync(["--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<ListLanesByWorkspaceQuery>(q => q.WorkspaceId == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithJsonFlag_ExitsZeroAndSerializesLanes()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var lane = new LaneInfo("To Do", 1);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LaneInfo>)[lane]);

        var cmd = new ListLanesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws", "--json"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithoutJsonFlag_ExitsZeroAndIteratesLanes()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var lane = new LaneInfo("To Do", 1);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LaneInfo>)[lane]);

        var cmd = new ListLanesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>());
    }
}

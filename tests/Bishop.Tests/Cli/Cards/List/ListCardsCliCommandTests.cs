using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.List;

public sealed class ListCardsCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsListCardsByWorkspaceQuery()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Card>)[]);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LaneInfo>)[]);

        var cmd = new ListCardsCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>());
    }
}

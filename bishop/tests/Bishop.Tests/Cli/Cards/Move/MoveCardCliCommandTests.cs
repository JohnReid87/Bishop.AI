using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.Move;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Move;

public sealed class MoveCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsMoveCardCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "Doing", Position = 0 };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);

        var cardResolver = new CardResolver(mediator);
        var cmd = new MoveCardCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["#1", "--to-lane", "Doing", "--to-position", "0", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>());
    }
}

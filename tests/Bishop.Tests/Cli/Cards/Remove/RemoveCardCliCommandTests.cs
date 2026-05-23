using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.Remove;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Remove;

public sealed class RemoveCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsRemoveCardCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var cardResolver = new CardResolver(mediator);
        var cmd = new RemoveCardCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>());
    }
}

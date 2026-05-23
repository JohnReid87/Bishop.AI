using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.Edit;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Edit;

public sealed class EditCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsUpdateCardCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Updated Title", LaneName = "To Do" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);

        var cardResolver = new CardResolver(mediator);
        var cmd = new EditCardCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["#1", "--title", "Updated Title", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }
}

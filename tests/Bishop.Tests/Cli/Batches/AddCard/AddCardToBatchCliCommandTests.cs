using Bishop.App.Batches.AddCardToBatch;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Batches.AddCard;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.AddCard;

[Collection("ConsoleTests")]
public sealed class AddCardToBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    private static IMediator MakeMediatorWithCard(Workspace ws, Card card)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        return mediator;
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsAddCommand()
    {
        var ws = MakeWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 12, Title = "Feature", LaneName = "To Do" };
        var mediator = MakeMediatorWithCard(ws, card);

        var cardResolver = new CardResolver(mediator);
        var cmd = new AddCardToBatchCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["my-batch", "#12", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<AddCardToBatchCommand>(c => c.BatchName == "my-batch" && c.CardId == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CardNotFound_ExitsOneAndDoesNotSendAddCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var cardResolver = new CardResolver(mediator);
        var cmd = new AddCardToBatchCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["my-batch", "#99", "--workspace", "test-ws"]);

        exitCode.Should().Be(1);
        await mediator.DidNotReceive().Send(
            Arg.Any<AddCardToBatchCommand>(),
            Arg.Any<CancellationToken>());
    }
}

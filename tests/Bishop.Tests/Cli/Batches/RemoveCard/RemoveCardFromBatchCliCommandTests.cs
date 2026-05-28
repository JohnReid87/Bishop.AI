using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Batches.RemoveCardFromBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Batches.RemoveCard;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.RemoveCard;

[Collection("ConsoleTests")]
public sealed class RemoveCardFromBatchCliCommandTests
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
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsRemoveCommand()
    {
        var ws = MakeWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 5, Title = "Task", LaneName = "Doing" };
        var mediator = MakeMediatorWithCard(ws, card);

        var cardResolver = new CardResolver(mediator);
        var cmd = new RemoveCardFromBatchCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["my-batch", "#5", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RemoveCardFromBatchCommand>(c => c.BatchName == "my-batch" && c.CardId == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CardNotFound_ExitsOneAndDoesNotSendRemoveCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var cardResolver = new CardResolver(mediator);
        var cmd = new RemoveCardFromBatchCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["my-batch", "#99", "--workspace", "test-ws"]);

        exitCode.Should().Be(1);
        await mediator.DidNotReceive().Send(
            Arg.Any<RemoveCardFromBatchCommand>(),
            Arg.Any<CancellationToken>());
    }
}

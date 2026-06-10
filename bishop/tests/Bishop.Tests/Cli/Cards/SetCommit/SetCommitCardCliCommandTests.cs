using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.SetCommit;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.SetCommit;

public sealed class SetCommitCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsSetCardCommitCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "Doing" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<SetCardCommitCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);

        var cardResolver = new CardResolver(mediator);
        var cmd = new SetCommitCardCliCommand(mediator, cardResolver);
        var exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws", "--hash", "abc12345def67890", "--branch", "main"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<SetCardCommitCommand>(), Arg.Any<CancellationToken>());
    }
}

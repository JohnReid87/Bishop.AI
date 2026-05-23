using Bishop.App.Cards.ClaimCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.Claim;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Claim;

public sealed class ClaimCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsClaimCardCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "Doing" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);

        var cmd = new ClaimCardCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>());
    }
}

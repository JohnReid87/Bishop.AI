using Bishop.App.WorkNext;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.WorkNext;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.WorkNext;

public sealed class WorkNextCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsWorkNextCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = Directory.GetCurrentDirectory() };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<WorkNextCommand>(), Arg.Any<CancellationToken>())
            .Returns(new WorkNextResult(1, WorkNextStopReason.EmptyLane));

        var cmd = new WorkNextCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--max", "1", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<WorkNextCommand>(), Arg.Any<CancellationToken>());
    }
}

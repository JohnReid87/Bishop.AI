using Bishop.App.Workspaces.InitWorkspace;
using Bishop.Cli.Workspaces.Init;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.Init;

public sealed class InitWorkspaceCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsInitWorkspaceCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: true, GitHubLinked: false));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--path", @"C:\test", "--name", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }
}

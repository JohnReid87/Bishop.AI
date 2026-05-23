using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.Cli.Workspaces.SetGitHub;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.SetGitHub;

public sealed class SetGitHubCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsSetWorkspaceGitHubRepoCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<SetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>())
            .Returns(ws);

        var cmd = new SetGitHubCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["owner/repo", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<SetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>());
    }
}

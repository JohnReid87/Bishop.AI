using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.Cli.Workspaces.UnsetGitHub;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.UnsetGitHub;

public sealed class UnsetGitHubCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsUnsetWorkspaceGitHubRepoCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<UnsetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>())
            .Returns(ws);

        var cmd = new UnsetGitHubCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<UnsetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>());
    }
}

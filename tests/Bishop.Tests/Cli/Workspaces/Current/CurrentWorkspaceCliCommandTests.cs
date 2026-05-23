using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Workspaces.Current;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.Current;

public sealed class CurrentWorkspaceCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndResolvesCurrentWorkspace()
    {
        var ws = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "test-ws",
            Path = Directory.GetCurrentDirectory()
        };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);

        var cmd = new CurrentWorkspaceCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>());
    }
}

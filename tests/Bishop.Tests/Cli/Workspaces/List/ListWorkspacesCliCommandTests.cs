using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Workspaces.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.List;

public sealed class ListWorkspacesCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsListWorkspacesQuery()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new ListWorkspacesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>());
    }
}

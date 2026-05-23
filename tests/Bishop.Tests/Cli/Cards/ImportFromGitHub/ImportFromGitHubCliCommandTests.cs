using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.ImportFromGitHub;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.ImportFromGitHub;

public sealed class ImportFromGitHubCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsImportFromGitHubCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var result = new ImportFromGitHubResult([], [], []);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ImportFromGitHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var cmd = new ImportFromGitHubCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ImportFromGitHubCommand>(), Arg.Any<CancellationToken>());
    }
}

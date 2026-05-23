using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Skills.Bootstrap;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Skills.Bootstrap;

public sealed class BootstrapSkillCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsGetSkillBootstrapInfoQuery()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = Directory.GetCurrentDirectory() };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetSkillBootstrapInfoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SkillBootstrapInfo("test-ws", ws.Path, null, [], []));

        var cmd = new BootstrapSkillCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<GetSkillBootstrapInfoQuery>(), Arg.Any<CancellationToken>());
    }
}

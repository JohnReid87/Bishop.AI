using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.RecordSkillRun;
using Bishop.Cli.Workspaces.RecordSkillRun;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.RecordSkillRun;

public sealed class RecordSkillRunCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsRecordSkillRunCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);

        var cmd = new RecordSkillRunCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--skill", "bish-arch", "--sha", "abc1234", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RecordSkillRunCommand>(c => c.WorkspaceId == ws.Id && c.SkillName == "bish-arch" && c.GitSha == "abc1234"),
            Arg.Any<CancellationToken>());
    }
}

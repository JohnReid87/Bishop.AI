using Bishop.App.Batches.FinishBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Finish;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Finish;

[Collection("ConsoleTests")]
public sealed class FinishBatchCliCommandTests
{
    private static Workspace MakeWorkspace(string? gitHubRepo = "owner/repo") =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject", GitHubRepo = gitHubRepo };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsFinishBatchCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<FinishBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new FinishBatchResult("https://github.com/owner/repo/pull/1"));

        var cmd = new FinishBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["Sprint 1", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<FinishBatchCommand>(c =>
                c.Name == "Sprint 1" &&
                c.WorkspacePath == ws.Path &&
                c.GitHubRepo == "owner/repo"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("https://github.com/owner/repo/pull/1");
    }

    [Fact]
    public async Task InvokeAsync_NoGitHubRepo_ExitsOneAndDoesNotSendFinishCommand()
    {
        var ws = MakeWorkspace(gitHubRepo: null);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);

        var cmd = new FinishBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["Sprint 1", "--workspace", "test-ws"]);

        exitCode.Should().Be(1);
        await mediator.DidNotReceive().Send(
            Arg.Any<FinishBatchCommand>(),
            Arg.Any<CancellationToken>());
    }
}

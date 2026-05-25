using Bishop.App.Batches.CompleteBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Complete;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Complete;

[Collection("ConsoleTests")]
public sealed class CompleteBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsCompleteCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);

        var cmd = new CompleteBatchCliCommand(mediator);

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
            Arg.Is<CompleteBatchCommand>(c => c.Name == "Sprint 1" && c.WorkspacePath == ws.Path),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("Batch completed.");
    }

    [Fact]
    public async Task InvokeAsync_WorkspaceNotFound_ExitsNonZero()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new CompleteBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["Sprint 1", "--workspace", "nonexistent-ws"]);

        exitCode.Should().NotBe(0);
        await mediator.DidNotReceive().Send(
            Arg.Any<CompleteBatchCommand>(),
            Arg.Any<CancellationToken>());
    }
}

using Bishop.App.Batches.AbandonBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Abandon;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Abandon;

[Collection("ConsoleTests")]
public sealed class AbandonBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsAbandonCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<AbandonBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AbandonBatchResult(4));

        var cmd = new AbandonBatchCliCommand(mediator);

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
            Arg.Is<AbandonBatchCommand>(c => c.Name == "Sprint 1" && c.WorkspacePath == ws.Path),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("4 card(s) restored");
    }

    [Fact]
    public async Task InvokeAsync_ZeroCardsRestored_ReportsZero()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<AbandonBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AbandonBatchResult(0));

        var cmd = new AbandonBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["Sprint 1", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("0 card(s) restored");
    }
}

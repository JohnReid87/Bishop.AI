using Bishop.App.Batches.DeleteBatchBranch;
using Bishop.App.Batches.GetBatchPruneCandidates;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Prune;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Prune;

[Collection("ConsoleTests")]
public sealed class PruneBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    private static PruneBatchCandidate MakeCandidate(string branch = "bishop/sprint-1", bool isCheckedOut = false) =>
        new("Sprint 1", branch, BatchClosedReason.Finished, DateTimeOffset.UtcNow.AddDays(-2), 5, isCheckedOut);

    private static IMediator MakeMediatorWithCandidates(
        Workspace ws,
        IReadOnlyList<PruneBatchCandidate> candidates)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetBatchPruneCandidatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
        return mediator;
    }

    [Fact]
    public async Task InvokeAsync_NoCandidates_PrintsNoCandidatesAndExitsZero()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);

        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No candidates found.");
        await mediator.DidNotReceive().Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DryRun_DoesNotSendDeleteCommand()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, [MakeCandidate()]);

        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--dry-run", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[dry-run]");
        await mediator.DidNotReceive().Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_YesFlag_DeletesAllPrunableCandidates()
    {
        var ws = MakeWorkspace();
        var candidate = MakeCandidate("bishop/sprint-1");
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        mediator.Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteBatchBranchResult());

        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--yes", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<DeleteBatchBranchCommand>(c =>
                c.WorkspacePath == ws.Path && c.BranchName == candidate.BranchName),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("1 branch(es) deleted.");
    }

    [Fact]
    public async Task InvokeAsync_AllCheckedOut_PrintsSkippedMessage()
    {
        var ws = MakeWorkspace();
        var candidate = MakeCandidate(isCheckedOut: true);
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);

        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--yes", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("currently checked out");
        await mediator.DidNotReceive().Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_InvalidOlderThan_SetsExitCodeOne()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);

        var cmd = new PruneBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["--older-than", "invalid", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }
    }

    [Theory]
    [InlineData("7d")]
    [InlineData("24h")]
    [InlineData("30m")]
    public async Task InvokeAsync_ValidOlderThan_PassesParsedDurationToQuery(string duration)
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);

        var cmd = new PruneBatchCliCommand(mediator);

        await cmd.InvokeAsync(["--older-than", duration, "--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.OlderThan.HasValue),
            Arg.Any<CancellationToken>());
    }
}

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
    public async Task InvokeAsync_NoCandidates_DoesNotSendDeleteCommand()
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
        await mediator.DidNotReceive().Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_YesFlag_SendsDeleteCommandForEachPrunableCandidate()
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
    }

    [Fact]
    public async Task InvokeAsync_AllCheckedOut_DoesNotSendDeleteCommand()
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
    [InlineData("7d", 7, 0, 0)]
    [InlineData("3h", 0, 3, 0)]
    [InlineData("30m", 0, 0, 30)]
    public async Task InvokeAsync_ValidOlderThan_PassesExactDurationToQuery(
        string duration, int days, int hours, int minutes)
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);
        var cmd = new PruneBatchCliCommand(mediator);
        var expected = new TimeSpan(days, hours, minutes, 0);

        await cmd.InvokeAsync(["--older-than", duration, "--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.OlderThan == expected),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("7")]    // single character: length < 2, no usable suffix
    [InlineData("-5d")]  // negative value
    [InlineData("7x")]   // unsupported suffix
    public async Task InvokeAsync_OlderThan_UnparsableDuration_SetsExitCodeOneAndDoesNotQuery(string duration)
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);
        var cmd = new PruneBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["--older-than", duration, "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }

        await mediator.DidNotReceive().Send(Arg.Any<GetBatchPruneCandidatesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AbandonedOnlyFlag_ForwardsAbandonedOnlyToQuery()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);
        var cmd = new PruneBatchCliCommand(mediator);

        await cmd.InvokeAsync(["--abandoned-only", "--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.AbandonedOnly && !q.MergedOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MergedOnlyFlag_ForwardsMergedOnlyToQuery()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);
        var cmd = new PruneBatchCliCommand(mediator);

        await cmd.InvokeAsync(["--merged-only", "--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => !q.AbandonedOnly && q.MergedOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_InteractiveAnswerYes_SendsDeleteCommand()
    {
        var ws = MakeWorkspace();
        var candidate = MakeCandidate("bishop/sprint-1");
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        mediator.Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteBatchBranchResult());
        var cmd = new PruneBatchCliCommand(mediator);

        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader("y\n"));
        Console.SetOut(new StringWriter());
        try
        {
            await cmd.InvokeAsync(["--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        await mediator.Received(1).Send(
            Arg.Is<DeleteBatchBranchCommand>(c =>
                c.WorkspacePath == ws.Path && c.BranchName == candidate.BranchName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_InteractiveAnswerNo_SkipsDeleteCommand()
    {
        var ws = MakeWorkspace();
        var candidate = MakeCandidate("bishop/sprint-1");
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        var cmd = new PruneBatchCliCommand(mediator);

        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader("n\n"));
        Console.SetOut(new StringWriter());
        try
        {
            await cmd.InvokeAsync(["--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        await mediator.DidNotReceive().Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CandidateOlderThanOneDay_OutputsAgeInDays()
    {
        var ws = MakeWorkspace();
        var candidate = new PruneBatchCandidate(
            "Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished,
            DateTimeOffset.UtcNow.AddDays(-3), 5, false);
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--dry-run", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("3d");
    }

    [Fact]
    public async Task InvokeAsync_CandidateOlderThanOneHour_OutputsAgeInHours()
    {
        var ws = MakeWorkspace();
        var candidate = new PruneBatchCandidate(
            "Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished,
            DateTimeOffset.UtcNow.AddHours(-5), 5, false);
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--dry-run", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("5h");
    }

    [Fact]
    public async Task InvokeAsync_CandidateOlderThanOneMinute_OutputsAgeInMinutes()
    {
        var ws = MakeWorkspace();
        var candidate = new PruneBatchCandidate(
            "Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished,
            DateTimeOffset.UtcNow.AddMinutes(-45), 5, false);
        var mediator = MakeMediatorWithCandidates(ws, [candidate]);
        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--dry-run", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("45m");
    }
}

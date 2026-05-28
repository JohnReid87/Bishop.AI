using Bishop.App.Batches.DeleteBatchBranch;
using Bishop.App.Batches.GetBatchPruneCandidates;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Prune;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
        output.ToString().Should().Contain("No candidates found.");
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
        output.ToString().Should().Contain("[dry-run] No changes made.");
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
        output.ToString().Should().Contain("1 branch(es) deleted.");
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
        output.ToString().Should().Contain("checked out — skipped");
        output.ToString().Should().Contain("All candidates are currently checked out");
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
    [InlineData("3h")]
    [InlineData("30m")]
    public async Task InvokeAsync_ValidOlderThan_ProceedsWithoutError(string duration)
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithCandidates(ws, []);
        var cmd = new PruneBatchCliCommand(mediator);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--older-than", duration, "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No candidates found.");
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

        var savedExitCode = Environment.ExitCode;
        var errorOutput = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(errorOutput);
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["--older-than", duration, "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = savedExitCode;
            Console.SetError(originalError);
        }

        await mediator.DidNotReceive().Send(Arg.Any<GetBatchPruneCandidatesQuery>(), Arg.Any<CancellationToken>());
        errorOutput.ToString().Should().Contain(duration);
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
        var output = new StringWriter();
        Console.SetIn(new StringReader("n\n"));
        Console.SetOut(output);
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
        output.ToString().Should().Contain("0 branch(es) deleted.");
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

    [Fact]
    public async Task InvokeAsync_AbandonedOnlyFlag_RenderedOutputDiffersFromNoFlag()
    {
        var ws = MakeWorkspace();
        var finishedCandidate = new PruneBatchCandidate(
            "Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished,
            DateTimeOffset.UtcNow.AddDays(-2), 5, false);
        var abandonedCandidate = new PruneBatchCandidate(
            "Sprint 2", "bishop/sprint-2", BatchClosedReason.Abandoned,
            DateTimeOffset.UtcNow.AddDays(-3), 2, false);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => !q.AbandonedOnly),
            Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PruneBatchCandidate>)[finishedCandidate, abandonedCandidate]);
        mediator.Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.AbandonedOnly),
            Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PruneBatchCandidate>)[abandonedCandidate]);

        var cmd = new PruneBatchCliCommand(mediator);

        var outputWithoutFlag = new StringWriter();
        var outputWithFlag = new StringWriter();
        var original = Console.Out;

        Console.SetOut(outputWithoutFlag);
        try { await cmd.InvokeAsync(["--dry-run", "--workspace", "test-ws"]); }
        finally { Console.SetOut(original); }

        Console.SetOut(outputWithFlag);
        try { await cmd.InvokeAsync(["--dry-run", "--abandoned-only", "--workspace", "test-ws"]); }
        finally { Console.SetOut(original); }

        outputWithoutFlag.ToString().Should().Contain("bishop/sprint-1");
        outputWithFlag.ToString().Should().NotContain("bishop/sprint-1");
        outputWithFlag.ToString().Should().Contain("bishop/sprint-2");
    }

    [Fact]
    public async Task InvokeAsync_MergedOnlyFlag_RenderedOutputDiffersFromAbandonedOnly()
    {
        var ws = MakeWorkspace();
        var abandonedCandidate = new PruneBatchCandidate(
            "Sprint A", "bishop/sprint-a", BatchClosedReason.Abandoned,
            DateTimeOffset.UtcNow.AddDays(-2), 3, false);
        var finishedCandidate = new PruneBatchCandidate(
            "Sprint B", "bishop/sprint-b", BatchClosedReason.Finished,
            DateTimeOffset.UtcNow.AddDays(-3), 5, false);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.AbandonedOnly),
            Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PruneBatchCandidate>)[abandonedCandidate]);
        mediator.Send(
            Arg.Is<GetBatchPruneCandidatesQuery>(q => q.MergedOnly),
            Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PruneBatchCandidate>)[finishedCandidate]);

        var cmd = new PruneBatchCliCommand(mediator);

        var outputAbandoned = new StringWriter();
        var outputMerged = new StringWriter();
        var original = Console.Out;

        Console.SetOut(outputAbandoned);
        try { await cmd.InvokeAsync(["--dry-run", "--abandoned-only", "--workspace", "test-ws"]); }
        finally { Console.SetOut(original); }

        Console.SetOut(outputMerged);
        try { await cmd.InvokeAsync(["--dry-run", "--merged-only", "--workspace", "test-ws"]); }
        finally { Console.SetOut(original); }

        outputAbandoned.ToString().Should().Contain("bishop/sprint-a");
        outputAbandoned.ToString().Should().NotContain("bishop/sprint-b");
        outputMerged.ToString().Should().Contain("bishop/sprint-b");
        outputMerged.ToString().Should().NotContain("bishop/sprint-a");
    }

    [Fact]
    public async Task InvokeAsync_WorkspaceNotFound_ExitsNonZero()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new PruneBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "nonexistent-ws"]);

        exitCode.Should().NotBe(0);
        await mediator.DidNotReceive().Send(
            Arg.Any<GetBatchPruneCandidatesQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_GetPruneCandidatesThrows_ExitsNonZero()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetBatchPruneCandidatesQuery>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        var cmd = new PruneBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task InvokeAsync_ThreeCandidatesOneCheckedOut_YesFlag_DeletesTwoBranches()
    {
        var ws = MakeWorkspace();
        var prunable1 = new PruneBatchCandidate("Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished, DateTimeOffset.UtcNow.AddDays(-2), 3, false);
        var prunable2 = new PruneBatchCandidate("Sprint 2", "bishop/sprint-2", BatchClosedReason.Abandoned, DateTimeOffset.UtcNow.AddDays(-5), 1, false);
        var checkedOut = new PruneBatchCandidate("Sprint 3", "bishop/sprint-3", BatchClosedReason.Finished, DateTimeOffset.UtcNow.AddDays(-1), 2, true);
        var mediator = MakeMediatorWithCandidates(ws, [prunable1, prunable2, checkedOut]);
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
        await mediator.Received(2).Send(Arg.Any<DeleteBatchBranchCommand>(), Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(
            Arg.Is<DeleteBatchBranchCommand>(c => c.BranchName == "bishop/sprint-3"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("2 branch(es) deleted.");
        output.ToString().Should().Contain("checked out — skipped");
    }

    [Theory]
    [InlineData(-1, 0, 0, "1d")]
    [InlineData(0, -1, 0, "1h")]
    [InlineData(0, 0, -1, "1m")]
    public async Task InvokeAsync_CandidateAtAgeBoundary_OutputsCorrectLabel(
        int daysOffset, int hoursOffset, int minutesOffset, string expectedLabel)
    {
        var ws = MakeWorkspace();
        var closedAt = DateTimeOffset.UtcNow
            .AddDays(daysOffset)
            .AddHours(hoursOffset)
            .AddMinutes(minutesOffset);
        var candidate = new PruneBatchCandidate("Sprint 1", "bishop/sprint-1", BatchClosedReason.Finished, closedAt, 3, false);
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

        output.ToString().Should().Contain(expectedLabel);
    }
}

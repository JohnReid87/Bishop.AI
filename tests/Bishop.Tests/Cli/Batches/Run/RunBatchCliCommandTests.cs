using Bishop.App.Batches.RunBatch;
using Bishop.App.Skills;
using Bishop.Cli.Batches.Run;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Run;

[Collection("ConsoleTests")]
public sealed class RunBatchCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(3, null, RunBatchStopReason.Finished));

        var cmd = new RunBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["Sprint 1"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RunBatchCommand>(c => c.Name == "Sprint 1" && !c.Resume && c.Model == SkillModelOptions.DefaultModelId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ResumeAndModel_PassesThroughToCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(0, null, RunBatchStopReason.Finished));

        var cmd = new RunBatchCliCommand(mediator);
        await cmd.InvokeAsync(["Sprint 1", "--resume", "--model", "claude-opus-4"]);

        await mediator.Received(1).Send(
            Arg.Is<RunBatchCommand>(c => c.Resume && c.Model == "claude-opus-4"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CardFailure_SetsExitCodeOne()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(1, [42], RunBatchStopReason.CardFailure));

        var cmd = new RunBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["Sprint 1"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }
    }

    [Fact]
    public async Task InvokeAsync_DirtyWorktree_SetsExitCodeOne()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(0, null, RunBatchStopReason.DirtyWorktree, [@"src/file.cs"]));

        var cmd = new RunBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["Sprint 1"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }
    }

    [Fact]
    public async Task InvokeAsync_NotAGitRepo_SetsExitCodeOne()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(0, null, RunBatchStopReason.NotAGitRepo));

        var cmd = new RunBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["Sprint 1"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }
    }

    [Fact]
    public async Task InvokeAsync_GitNotFound_SetsExitCodeOne()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RunBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RunBatchResult(0, null, RunBatchStopReason.GitNotFound));

        var cmd = new RunBatchCliCommand(mediator);

        var saved = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await cmd.InvokeAsync(["Sprint 1"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = saved;
        }
    }
}

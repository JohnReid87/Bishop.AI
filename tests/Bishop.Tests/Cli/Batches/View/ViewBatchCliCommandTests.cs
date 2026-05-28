using Bishop.App.Batches.GetBatch;
using Bishop.Cli.Batches.View;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.View;

[Collection("ConsoleTests")]
public sealed class ViewBatchCliCommandTests
{
    private static Batch MakeBatch(string name = "Sprint 1") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BranchName = "bishop/sprint-1",
        BaseBranch = "main",
        Status = BatchStatus.Open,
        WorktreePath = @"C:\repos\MyProject-bishop-worktrees\sprint-1",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsGetBatchQuery()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(MakeBatch(), []));

        var cmd = new ViewBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["Sprint 1"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<GetBatchQuery>(q => q.Name == "Sprint 1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsJsonContainingBatchName()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(MakeBatch("my-batch"), []));

        var cmd = new ViewBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["my-batch", "--json"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\"my-batch\"");
    }

    [Fact]
    public async Task InvokeAsync_NoCards_PrintsNoneMessage()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(MakeBatch(), []));

        var cmd = new ViewBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["Sprint 1"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("(none)");
    }

    [Fact]
    public async Task InvokeAsync_WithCards_PrintsCardNumber()
    {
        var mediator = Substitute.For<IMediator>();
        var batch = MakeBatch();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Number = 7,
            Title = "My task",
            LaneName = "Doing"
        };
        mediator.Send(Arg.Any<GetBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetBatchResult(batch, [card]));

        var cmd = new ViewBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["Sprint 1"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("#7");
        output.ToString().Should().Contain("My task");
    }
}

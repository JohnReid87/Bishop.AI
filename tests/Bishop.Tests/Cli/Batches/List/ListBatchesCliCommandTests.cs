using Bishop.App.Batches.ListBatches;
using Bishop.Cli.Batches.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.List;

[Collection("ConsoleTests")]
public sealed class ListBatchesCliCommandTests
{
    private static Batch MakeBatch(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BranchName = $"bishop/{name}",
        BaseBranch = "main",
        Status = BatchStatus.Open,
        WorktreePath = @"C:\repos\proj-bishop-worktrees\batch",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task InvokeAsync_NoBatches_PrintsNoActiveBatches()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<BatchSummary>)[]);

        var cmd = new ListBatchesCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync([]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No active batches.");
    }

    [Fact]
    public async Task InvokeAsync_WithBatches_PrintsBatchNames()
    {
        var mediator = Substitute.For<IMediator>();
        var summaries = new List<BatchSummary>
        {
            new(MakeBatch("sprint-1"), 3),
            new(MakeBatch("sprint-2"), 1)
        };
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<BatchSummary>)summaries);

        var cmd = new ListBatchesCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync([]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("sprint-1");
        output.ToString().Should().Contain("sprint-2");
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsJsonArray()
    {
        var mediator = Substitute.For<IMediator>();
        var summaries = new List<BatchSummary>
        {
            new(MakeBatch("my-batch"), 2)
        };
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<BatchSummary>)summaries);

        var cmd = new ListBatchesCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--json"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\"my-batch\"");
    }
}

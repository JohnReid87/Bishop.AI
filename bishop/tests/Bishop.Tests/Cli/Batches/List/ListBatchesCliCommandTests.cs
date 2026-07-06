using Bishop.App.Batches.ListBatches;
using Bishop.App.Workspaces.ListWorkspaces;
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
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

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

    private static IMediator MediatorReturning(IReadOnlyList<BatchSummary> summaries)
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListBatchesQuery>(), Arg.Any<CancellationToken>())
            .Returns(summaries);
        return mediator;
    }

    [Fact]
    public async Task InvokeAsync_NoBatches_PrintsNoActiveBatches()
    {
        var cmd = new ListBatchesCliCommand(MediatorReturning([]));

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
        output.ToString().Should().Contain("No active batches.");
    }

    [Fact]
    public async Task InvokeAsync_WithBatches_PrintsBatchNames()
    {
        var summaries = new List<BatchSummary>
        {
            new(MakeBatch("sprint-1"), 3, null, null, false, false, false, []),
            new(MakeBatch("sprint-2"), 1, null, null, false, false, false, [])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

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
        output.ToString().Should().Contain("sprint-1");
        output.ToString().Should().Contain("sprint-2");
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsJsonArray()
    {
        var summaries = new List<BatchSummary>
        {
            new(MakeBatch("my-batch"), 2, null, null, false, false, false, [])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--json", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\"my-batch\"");
    }

    [Fact]
    public async Task InvokeAsync_WorkingBatchWithFinishedAt_RendersFinished()
    {
        var batch = MakeBatch("done-run");
        batch.Status = BatchStatus.Working;
        batch.FinishedAt = DateTimeOffset.UtcNow;
        var summaries = new List<BatchSummary>
        {
            new(batch, 0, batch.FinishedAt, null, false, false, false, [])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

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
        output.ToString().Should().Contain("Finished");
        output.ToString().Should().NotContain("Working");
    }

    [Fact]
    public async Task InvokeAsync_HandWorkedBatchAllCardsDone_RendersFinished()
    {
        var batch = MakeBatch("hand-worked");
        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Number = 1,
            Title = "task",
            LaneName = SystemLaneNames.Done
        };
        var summaries = new List<BatchSummary>
        {
            new(batch, 1, null, null, false, false, false, [card])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

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
        output.ToString().Should().Contain("Finished");
    }

    [Fact]
    public async Task InvokeAsync_MergedBatch_RendersMerged()
    {
        var batch = MakeBatch("merged-run");
        batch.Status = BatchStatus.Working;
        batch.MergedAt = DateTimeOffset.UtcNow;
        var summaries = new List<BatchSummary>
        {
            new(batch, 0, null, batch.MergedAt, true, false, false, [])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

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
        output.ToString().Should().Contain("Merged");
    }

    [Fact]
    public async Task InvokeAsync_Default_QueriesWithIncludeClosedFalse()
    {
        var mediator = MediatorReturning([]);
        var cmd = new ListBatchesCliCommand(mediator);

        var original = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            await cmd.InvokeAsync(["--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        await mediator.Received().Send(
            Arg.Is<ListBatchesQuery>(q => !q.IncludeClosed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AllFlag_QueriesWithIncludeClosedTrue()
    {
        var mediator = MediatorReturning([]);
        var cmd = new ListBatchesCliCommand(mediator);

        var original = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            await cmd.InvokeAsync(["--all", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        await mediator.Received().Send(
            Arg.Is<ListBatchesQuery>(q => q.IncludeClosed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_KeepsRawStatusAndAddsDerivedDisplayState()
    {
        var batch = MakeBatch("merged-run");
        batch.Status = BatchStatus.Working;
        batch.MergedAt = DateTimeOffset.UtcNow;
        var summaries = new List<BatchSummary>
        {
            new(batch, 0, null, batch.MergedAt, true, false, false, [])
        };
        var cmd = new ListBatchesCliCommand(MediatorReturning(summaries));

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--json", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\"status\": \"Working\"");
        output.ToString().Should().Contain("\"displayState\": \"Merged\"");
    }
}

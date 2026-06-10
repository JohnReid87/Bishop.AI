using Bishop.App.Batches.RenameBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Edit;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Edit;

[Collection("ConsoleTests")]
public sealed class EditBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    private static Batch MakeBatch(string name) =>
        new() { Id = Guid.NewGuid(), Name = name, BranchName = "bishop/sprint-1", BaseBranch = "main", WorktreePath = @"C:\worktrees\sprint-1", Status = BatchStatus.Open };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsRenameCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<RenameBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(MakeBatch("Sprint 2"));

        var cmd = new EditBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["Sprint 1", "--new-name", "Sprint 2", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RenameBatchCommand>(c => c.Name == "Sprint 1" && c.NewName == "Sprint 2"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("Renamed 'Sprint 1' → 'Sprint 2'.");
    }

    [Fact]
    public async Task InvokeAsync_WorkspaceNotFound_ExitsNonZero()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new EditBatchCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["Sprint 1", "--new-name", "Sprint 2", "--workspace", "no-such-ws"]);

        exitCode.Should().NotBe(0);
        await mediator.DidNotReceive().Send(Arg.Any<RenameBatchCommand>(), Arg.Any<CancellationToken>());
    }
}

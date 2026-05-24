using Bishop.App.Batches.CreateBatch;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Batches.Create;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Create;

[Collection("ConsoleTests")]
public sealed class CreateBatchCliCommandTests
{
    private static Workspace MakeWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\repos\MyProject" };

    private static IMediator MakeMediatorWithWorkspace(Workspace ws)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        return mediator;
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsCommand()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithWorkspace(ws);
        var batch = new Batch { Id = Guid.NewGuid(), Name = "Sprint 1", BranchName = "bishop/sprint-1", BaseBranch = "main", WorktreePath = @"C:\repos\MyProject-bishop-worktrees\sprint-1", Status = BatchStatus.Open };
        mediator.Send(Arg.Any<CreateBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateBatchResult(batch, 0));

        var cmd = new CreateBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--name", "Sprint 1", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Is<CreateBatchCommand>(c =>
            c.Name == "Sprint 1" &&
            c.BranchName == "bishop/sprint-1" &&
            c.WorkspaceId == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CustomBranch_PassesThrough()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithWorkspace(ws);
        var batch = new Batch { Id = Guid.NewGuid(), Name = "My Batch", BranchName = "custom/branch", BaseBranch = "main", WorktreePath = @"C:\repos\MyProject-bishop-worktrees\my-batch", Status = BatchStatus.Open };
        mediator.Send(Arg.Any<CreateBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateBatchResult(batch, 0));

        var cmd = new CreateBatchCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--name", "My Batch", "--branch", "custom/branch", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<CreateBatchCommand>(c => c.BranchName == "custom/branch"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SlugifiesSpacesAndSpecialChars()
    {
        var ws = MakeWorkspace();
        var mediator = MakeMediatorWithWorkspace(ws);
        var batch = new Batch { Id = Guid.NewGuid(), Name = "Hello World!", BranchName = "bishop/hello-world", BaseBranch = "main", WorktreePath = @"C:\worktrees\hello-world", Status = BatchStatus.Open };
        mediator.Send(Arg.Any<CreateBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateBatchResult(batch, 0));

        var cmd = new CreateBatchCliCommand(mediator);
        await cmd.InvokeAsync(["--name", "Hello World!", "--workspace", "test-ws"]);

        await mediator.Received(1).Send(
            Arg.Is<CreateBatchCommand>(c => c.BranchName == "bishop/hello-world"),
            Arg.Any<CancellationToken>());
    }
}

using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Git;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Batches;

public sealed class CleanUpBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public CleanUpBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateWorkingBatchAsync()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), $"bishop/{U("br")}", "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private static IGitCli GitMergedNoBranchNoWorktree()
    {
        var git = Substitute.For<IGitCli>();
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        git.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        git.GetWorktreeBranchesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);
        git.RemoveWorktreeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return git;
    }

    private CleanUpBatchCommandHandler CreateHandler(IGitCli? git = null)
        => new(new BatchRepository(_factory), git ?? GitMergedNoBranchNoWorktree(),
               NullLogger<CleanUpBatchCommandHandler>.Instance);

    // ── guard: batch not found ─────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new CleanUpBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    // ── guard: not merged ──────────────────────────────────────────────────────

    [Fact]
    public async Task NotMerged_Throws()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = Substitute.For<IGitCli>();
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Func<Task> act = () => CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not been merged*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ClosesBatch_WithFinishedReason()
    {
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler().Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Finished);
        saved.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BranchExists_DeletesBranch()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.LocalBranchExistsAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>())
            .Returns(true);
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).DeleteLocalBranchAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BranchCheckedOut_SkipsBranchDeletion()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.LocalBranchExistsAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>())
            .Returns(true);
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[batch.BranchName]);

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await git.DidNotReceive().DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorktreeRemoveFails_BatchStillClosed()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.RemoveWorktreeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("worktree not found"));

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
    }
}

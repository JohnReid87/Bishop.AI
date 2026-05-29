using Bishop.App.Batches.GetBatchPruneCandidates;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class GetBatchPruneCandidatesQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorkspacePath = @"C:\fake-workspace";

    public GetBatchPruneCandidatesQueryHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateClosedBatchAsync(
        BatchClosedReason reason, DateTimeOffset? closedAt = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = U("batch"),
            BranchName = $"bishop/{U("br")}",
            BaseBranch = "main",
            WorktreePath = @"C:\wt",
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        batch.Close(reason, DateTimeOffset.UtcNow);
        if (closedAt.HasValue)
            batch.ClosedAt = closedAt.Value;
        await db.SaveChangesAsync();
        return batch;
    }

    private GetBatchPruneCandidatesQueryHandler CreateHandler(IGitCli git)
        => new(_factory, git);

    private static IGitCli GitWhere(string branchName, bool exists, int commitCount = 0, bool checkedOut = false)
    {
        var git = Substitute.For<IGitCli>();
        git.LocalBranchExistsAsync(WorkspacePath, branchName, Arg.Any<CancellationToken>())
            .Returns(exists);
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(checkedOut ? [branchName] : []);
        git.GetBranchCommitCountAsync(WorkspacePath, branchName, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(commitCount);
        return git;
    }

    // ── filtering ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoCandidates_WhenNoBatchesExist()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, null), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludesOpenBatches()
    {
        await using var dbSetup = await _factory.CreateDbContextAsync();
        var open = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = $"bishop/{U("br")}",
            BaseBranch = "main", WorktreePath = @"C:\wt", Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        dbSetup.Batches.Add(open);
        await dbSetup.SaveChangesAsync();

        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);
        git.LocalBranchExistsAsync(WorkspacePath, open.BranchName, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, null), default);

        result.Should().NotContain(c => c.BranchName == open.BranchName);
    }

    [Fact]
    public async Task ExcludesBatchesWhoseBranchDoesNotExistLocally()
    {
        var batch = await CreateClosedBatchAsync(BatchClosedReason.Abandoned);

        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);
        git.LocalBranchExistsAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, null), default);

        result.Should().NotContain(c => c.BranchName == batch.BranchName);
    }

    [Fact]
    public async Task AbandonedOnlyFilter_ExcludesFinished()
    {
        var abandoned = await CreateClosedBatchAsync(BatchClosedReason.Abandoned);
        var finished = await CreateClosedBatchAsync(BatchClosedReason.Finished);

        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);
        git.LocalBranchExistsAsync(WorkspacePath, abandoned.BranchName, Arg.Any<CancellationToken>()).Returns(true);
        git.LocalBranchExistsAsync(WorkspacePath, finished.BranchName, Arg.Any<CancellationToken>()).Returns(true);
        git.GetBranchCommitCountAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, AbandonedOnly: true, MergedOnly: false, OlderThan: null), default);

        result.Should().ContainSingle(c => c.BranchName == abandoned.BranchName);
        result.Should().NotContain(c => c.BranchName == finished.BranchName);
    }

    [Fact]
    public async Task MergedOnlyFilter_ExcludesAbandoned()
    {
        var abandoned = await CreateClosedBatchAsync(BatchClosedReason.Abandoned);
        var finished = await CreateClosedBatchAsync(BatchClosedReason.Finished);

        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);
        git.LocalBranchExistsAsync(WorkspacePath, abandoned.BranchName, Arg.Any<CancellationToken>()).Returns(true);
        git.LocalBranchExistsAsync(WorkspacePath, finished.BranchName, Arg.Any<CancellationToken>()).Returns(true);
        git.GetBranchCommitCountAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, AbandonedOnly: false, MergedOnly: true, OlderThan: null), default);

        result.Should().ContainSingle(c => c.BranchName == finished.BranchName);
        result.Should().NotContain(c => c.BranchName == abandoned.BranchName);
    }

    [Fact]
    public async Task OlderThanFilter_ExcludesTooRecent()
    {
        var recent = await CreateClosedBatchAsync(BatchClosedReason.Abandoned, DateTimeOffset.UtcNow.AddHours(-1));

        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);
        git.LocalBranchExistsAsync(WorkspacePath, recent.BranchName, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, TimeSpan.FromDays(7)), default);

        result.Should().NotContain(c => c.BranchName == recent.BranchName);
    }

    [Fact]
    public async Task OlderThanFilter_IncludesOldEnough()
    {
        var old = await CreateClosedBatchAsync(BatchClosedReason.Abandoned, DateTimeOffset.UtcNow.AddDays(-8));

        var git = GitWhere(old.BranchName, exists: true, commitCount: 3);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, TimeSpan.FromDays(7)), default);

        result.Should().ContainSingle(c => c.BranchName == old.BranchName);
    }

    // ── candidate metadata ────────────────────────────────────────────────────

    [Fact]
    public async Task IsCheckedOut_True_WhenBranchInWorktrees()
    {
        var batch = await CreateClosedBatchAsync(BatchClosedReason.Abandoned);

        var git = GitWhere(batch.BranchName, exists: true, commitCount: 2, checkedOut: true);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, null), default);

        result.Should().ContainSingle(c => c.BranchName == batch.BranchName && c.IsCheckedOut);
    }

    [Fact]
    public async Task CommitCount_PopulatedFromGit()
    {
        var batch = await CreateClosedBatchAsync(BatchClosedReason.Finished);

        var git = GitWhere(batch.BranchName, exists: true, commitCount: 7);

        var result = await CreateHandler(git).Handle(
            new GetBatchPruneCandidatesQuery(_wsId, WorkspacePath, false, false, null), default);

        result.Should().ContainSingle(c => c.BranchName == batch.BranchName && c.CommitCount == 7);
    }
}

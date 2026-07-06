using Bishop.App.Batches.MergeBatch;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class MergeBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\merge-batch";

    public MergeBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateWorkingBatchAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = U("batch"),
            BranchName = $"bishop/{U("br")}",
            BaseBranch = "main",
            WorktreePath = WorktreePath,
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        await db.SaveChangesAsync();
        return batch;
    }

    private static IGitCli GitMergeSucceeds(string currentBranch = "main")
    {
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(currentBranch);
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(true, []));
        return git;
    }

    private MergeBatchCommandHandler CreateHandler(IGitCli? git = null) =>
        new(_factory, git ?? GitMergeSucceeds(), TimeProvider.System);

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new MergeBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task BatchOpen_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler().Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        batch.Close(BatchClosedReason.Finished, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler().Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    // ── merge conflict ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MergeConflict_ReturnsConflictFiles()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(false, ["src/Foo.cs", "src/Bar.cs"]));

        var result = await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        result.Success.Should().BeFalse();
        result.ConflictFiles.Should().BeEquivalentTo(["src/Foo.cs", "src/Bar.cs"]);
    }

    [Fact]
    public async Task MergeConflict_DoesNotChangeBatchStatus()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(false, ["src/Foo.cs"]));

        await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task WrongBranch_ReturnsFailureWithDescriptiveMessage()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = GitMergeSucceeds(currentBranch: "feature/other");

        var result = await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        result.Success.Should().BeFalse();
        result.ConflictFiles.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("feature/other").And.Contain("main");
    }

    [Fact]
    public async Task WrongBranch_DoesNotCallMerge()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = GitMergeSucceeds(currentBranch: "feature/other");

        await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        await git.DidNotReceive().MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonConflictGitError_PropagatesErrorMessage()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(false, [], "fatal: already up to date."));

        var result = await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        result.Success.Should().BeFalse();
        result.ConflictFiles.Should().BeEmpty();
        result.ErrorMessage.Should().Be("fatal: already up to date.");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ReturnsEmptyConflictFiles()
    {
        var batch = await CreateWorkingBatchAsync();

        var result = await CreateHandler().Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        result.Success.Should().BeTrue();
        result.ConflictFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_DoesNotCloseBatch()
    {
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler().Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Success_StampsMergedAt()
    {
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler().Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.MergedAt.Should().NotBeNull();
        saved.Status.Should().Be(BatchStatus.Working);
    }

    [Fact]
    public async Task MergeConflict_DoesNotStampMergedAt()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(false, ["src/Foo.cs"]));

        await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.MergedAt.Should().BeNull();
    }

    [Fact]
    public async Task Success_PassesBranchNameToGit()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = GitMergeSucceeds();

        await CreateHandler(git).Handle(new MergeBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).MergeAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>());
    }
}

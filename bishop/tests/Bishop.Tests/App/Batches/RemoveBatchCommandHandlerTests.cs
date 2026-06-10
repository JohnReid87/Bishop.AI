using Bishop.App.Batches.RemoveBatch;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class RemoveBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public RemoveBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateClosedBatchAsync()
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
        batch.Close(BatchClosedReason.Finished, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        return batch;
    }

    private RemoveBatchCommandHandler CreateHandler()
        => new(_factory);

    // ── guard: batch not found ─────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new RemoveBatchCommand("no-such"), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    // ── guard: not closed ──────────────────────────────────────────────────────

    [Fact]
    public async Task OpenBatch_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = $"bishop/{U("br")}",
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler().Handle(new RemoveBatchCommand(batch.Name), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be Closed*");
    }

    [Fact]
    public async Task WorkingBatch_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = $"bishop/{U("br")}",
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler().Handle(new RemoveBatchCommand(batch.Name), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be Closed*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClosedBatch_DeletesRecord()
    {
        var batch = await CreateClosedBatchAsync();

        await CreateHandler().Handle(new RemoveBatchCommand(batch.Name), default);

        var deleted = await _db.Batches.FindAsync(batch.Id);
        deleted.Should().BeNull();
    }
}

using Bishop.App.Batches.RenameBatch;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class RenameBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorktreePath = @"C:\fake-worktrees\rename-batch";

    public RenameBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateBatchAsync(string? name = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = name ?? U("batch"),
            BranchName = U("br"),
            BaseBranch = "main",
            WorktreePath = WorktreePath,
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private RenameBatchCommandHandler CreateHandler() => new(_factory);

    // ── validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new RenameBatchCommand("no-such-batch", "new-name"), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task EmptyNewName_Throws()
    {
        var batch = await CreateBatchAsync();

        Func<Task> act = () => CreateHandler().Handle(new RenameBatchCommand(batch.Name, "   "), default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public async Task NewNameConflictsWithActiveBatch_Throws()
    {
        var existing = await CreateBatchAsync();
        var target = await CreateBatchAsync();

        Func<Task> act = () => CreateHandler().Handle(new RenameBatchCommand(target.Name, existing.Name), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*'{existing.Name}'*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidRename_PersistsNewName()
    {
        var batch = await CreateBatchAsync();
        var newName = U("renamed");

        await CreateHandler().Handle(new RenameBatchCommand(batch.Name, newName), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Name.Should().Be(newName);
    }

    [Fact]
    public async Task ValidRename_ReturnsBatchWithNewName()
    {
        var batch = await CreateBatchAsync();
        var newName = U("renamed");

        var result = await CreateHandler().Handle(new RenameBatchCommand(batch.Name, newName), default);

        result.Name.Should().Be(newName);
        result.Id.Should().Be(batch.Id);
    }

    [Fact]
    public async Task SameName_ReturnsBatchUnchanged()
    {
        var batch = await CreateBatchAsync();

        var result = await CreateHandler().Handle(new RenameBatchCommand(batch.Name, batch.Name), default);

        result.Name.Should().Be(batch.Name);
        result.Id.Should().Be(batch.Id);
    }

    [Fact]
    public async Task NewNameTrimsWhitespace()
    {
        var batch = await CreateBatchAsync();
        var newName = U("renamed");

        var result = await CreateHandler().Handle(new RenameBatchCommand(batch.Name, $"  {newName}  "), default);

        result.Name.Should().Be(newName);
    }

    [Fact]
    public async Task ConflictCheckIgnoresClosedBatches()
    {
        var closed = await CreateBatchAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var batchToClose = await db.Batches.FindAsync(closed.Id);
        batchToClose!.TransitionToWorking();
        batchToClose.Close(BatchClosedReason.Finished, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var target = await CreateBatchAsync();

        var result = await CreateHandler().Handle(new RenameBatchCommand(target.Name, closed.Name), default);

        result.Name.Should().Be(closed.Name);
    }
}

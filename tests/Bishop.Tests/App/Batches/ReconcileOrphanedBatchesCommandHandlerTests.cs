using Bishop.App.Batches.ReconcileOrphanedBatches;
using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class ReconcileOrphanedBatchesCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _worktreePath;

    public ReconcileOrphanedBatchesCommandHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _worktreePath = Path.Combine(Path.GetTempPath(), "bishop-reconcile-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_worktreePath, ".bishop"));
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Batch batch, Card card)> SetupWorkingBatchWithCardAsync(string cardLaneName = SystemLaneNames.Doing)
    {
        var wsName = U("ws");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(wsName, $@"C:\{wsName}"), default);

        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, U("card")), default);

        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), $"bishop/{U("br")}", "main", _worktreePath);
        await repo.AssignCardAsync(batch.Id, card.Id);
        batch = await repo.TransitionToWorkingAsync(batch.Id);

        await using var db = await _factory.CreateDbContextAsync(default);
        var dbCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        dbCard.LaneName = cardLaneName;
        await db.SaveChangesAsync();

        return (batch, card);
    }

    private void WriteLockFile(Guid batchId, int pid, DateTimeOffset timestamp)
    {
        var path = Path.Combine(_worktreePath, ".bishop", $"batch-{batchId}.lock");
        File.WriteAllText(path, $"{pid}\t{timestamp:O}");
    }

    private ReconcileOrphanedBatchesCommandHandler CreateHandler() =>
        new(new BatchRepository(_factory), _factory);

    // ── no Working batches ─────────────────────────────────────────────────────

    [Fact]
    public async Task NoBatchesInWorking_DoesNothing()
    {
        await using var dbBefore = await _factory.CreateDbContextAsync(default);
        var failedCountBefore = await dbBefore.Cards.CountAsync(c => c.LastAutoRunFailedAt != null);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var dbAfter = await _factory.CreateDbContextAsync(default);
        var failedCountAfter = await dbAfter.Cards.CountAsync(c => c.LastAutoRunFailedAt != null);
        failedCountAfter.Should().Be(failedCountBefore);
    }

    // ── orphan detection: missing lock file ────────────────────────────────────

    [Fact]
    public async Task MissingLockFile_SetsLastAutoRunFailedAt_OnDoingCard()
    {
        var (_, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var updated = await db.Cards.FirstAsync(c => c.Id == card.Id);
        updated.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── orphan detection: stale lock file ─────────────────────────────────────

    [Fact]
    public async Task StaleLockFile_SetsLastAutoRunFailedAt()
    {
        var (batch, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        WriteLockFile(batch.Id, Environment.ProcessId, DateTimeOffset.UtcNow.AddMinutes(-10));

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var updated = await db.Cards.FirstAsync(c => c.Id == card.Id);
        updated.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── orphan detection: dead PID ─────────────────────────────────────────────

    [Fact]
    public async Task DeadPidInLockFile_SetsLastAutoRunFailedAt()
    {
        var (batch, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        // int.MaxValue is virtually guaranteed to have no running process on Windows
        WriteLockFile(batch.Id, int.MaxValue, DateTimeOffset.UtcNow);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var updated = await db.Cards.FirstAsync(c => c.Id == card.Id);
        updated.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── orphan detection: unreadable lock file ────────────────────────────────

    [Fact]
    public async Task UnreadableLockFile_SetsLastAutoRunFailedAt()
    {
        var (batch, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        var lockPath = Path.Combine(_worktreePath, ".bishop", $"batch-{batch.Id}.lock");

        // Hold an exclusive file lock so File.ReadAllText throws — exercises the IsOrphaned catch block.
        await using var lockedFile = new FileStream(lockPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await lockedFile.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"{Environment.ProcessId}\t{DateTimeOffset.UtcNow:O}"));
        await lockedFile.FlushAsync();

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var updated = await db.Cards.FirstAsync(c => c.Id == card.Id);
        updated.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── orphan detection: invalid PID ─────────────────────────────────────────

    [Fact]
    public async Task InvalidPidInLockFile_SetsLastAutoRunFailedAt()
    {
        var (batch, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        // -1 is an out-of-range PID; Process.GetProcessById throws — exercises the IsProcessAlive catch block.
        WriteLockFile(batch.Id, -1, DateTimeOffset.UtcNow);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var updated = await db.Cards.FirstAsync(c => c.Id == card.Id);
        updated.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── live batch: fresh lock with alive PID ─────────────────────────────────

    [Fact]
    public async Task FreshLockFileWithLivePid_DoesNotMarkAsOrphaned()
    {
        var (batch, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        WriteLockFile(batch.Id, Environment.ProcessId, DateTimeOffset.UtcNow);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var unchanged = await db.Cards.FirstAsync(c => c.Id == card.Id);
        unchanged.LastAutoRunFailedAt.Should().BeNull();
    }

    // ── idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AlreadyFailedCard_IsNotUpdatedAgain()
    {
        var (_, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.Doing);
        var originalTime = DateTimeOffset.UtcNow.AddHours(-1);

        await using var setup = await _factory.CreateDbContextAsync(default);
        var dbCard = await setup.Cards.FirstAsync(c => c.Id == card.Id);
        dbCard.LastAutoRunFailedAt = originalTime;
        await setup.SaveChangesAsync();

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var unchanged = await db.Cards.FirstAsync(c => c.Id == card.Id);
        unchanged.LastAutoRunFailedAt.Should().Be(originalTime);
    }

    // ── card not in Doing ─────────────────────────────────────────────────────

    [Fact]
    public async Task CardNotInDoing_IsNotAffected()
    {
        var (_, card) = await SetupWorkingBatchWithCardAsync(SystemLaneNames.ToDo);

        await CreateHandler().Handle(new ReconcileOrphanedBatchesCommand(), default);

        await using var db = await _factory.CreateDbContextAsync(default);
        var unchanged = await db.Cards.FirstAsync(c => c.Id == card.Id);
        unchanged.LastAutoRunFailedAt.Should().BeNull();
    }
}

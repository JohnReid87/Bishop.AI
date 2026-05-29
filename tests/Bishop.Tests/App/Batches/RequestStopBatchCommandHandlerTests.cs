using Bishop.App.Batches.RequestStopBatch;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class RequestStopBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorktreePath = @"C:\fake-worktrees\stop-batch";

    public RequestStopBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateWorkingBatchAsync()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private RequestStopBatchCommandHandler CreateHandler() =>
        new(_factory);

    // ── validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new RequestStopBatchCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task BatchOpen_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);

        Func<Task> act = () => CreateHandler().Handle(new RequestStopBatchCommand(batch.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        Func<Task> act = () => CreateHandler().Handle(new RequestStopBatchCommand(batch.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WorkingBatch_SetsStoppedAt()
    {
        var batch = await CreateWorkingBatchAsync();
        var before = DateTimeOffset.UtcNow;

        await CreateHandler().Handle(new RequestStopBatchCommand(batch.Id), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.StoppedAt.Should().NotBeNull();
        saved.StoppedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task WorkingBatch_StatusRemainsWorking()
    {
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler().Handle(new RequestStopBatchCommand(batch.Id), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
    }
}

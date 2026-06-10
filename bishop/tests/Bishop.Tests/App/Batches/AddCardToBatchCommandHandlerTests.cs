using Bishop.App.Batches.AddCardToBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class AddCardToBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public AddCardToBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private AddCardToBatchCommandHandler Handler() => new(_factory);

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId)
        => await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, SystemLaneNames.ToDo, U("card")), default);

    private async Task<Batch> CreateBatchAsync(string name, string branch, BatchStatus status = BatchStatus.Open)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = name,
            BranchName = branch,
            BaseBranch = "main",
            WorktreePath = WorktreePath,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private async Task TransitionToWorkingAsync(Guid batchId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = await db.Batches.FindAsync(batchId);
        batch!.TransitionToWorking();
        await db.SaveChangesAsync();
    }

    private async Task CloseAsync(Guid batchId, BatchClosedReason reason)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = await db.Batches.FindAsync(batchId);
        batch!.Close(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => Handler().Handle(new AddCardToBatchCommand("no-such-batch", Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task MultipleBatchesSameName_Throws()
    {
        var name = U("batch");
        await CreateBatchAsync(name, U("br1"));
        await CreateBatchAsync(name, U("br2"));

        Func<Task> act = () => Handler().Handle(new AddCardToBatchCommand(name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple*");
    }

    [Fact]
    public async Task BatchWorking_Throws()
    {
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await TransitionToWorkingAsync(batch.Id);

        Func<Task> act = () => Handler().Handle(new AddCardToBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await TransitionToWorkingAsync(batch.Id);
        await CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        Func<Task> act = () => Handler().Handle(new AddCardToBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public async Task OpenBatch_AssignsCard()
    {
        var ws = await CreateWorkspaceAsync();
        var card = await AddCardAsync(ws.Id);
        var batch = await CreateBatchAsync(U("batch"), U("br"));

        await Handler().Handle(new AddCardToBatchCommand(batch.Name, card.Id), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.BatchId.Should().Be(batch.Id);
    }

    [Fact]
    public async Task CardAlreadyInOpenBatch_Throws()
    {
        // Arrange — card assigned to batch1 (Open)
        var ws = await CreateWorkspaceAsync();
        var card = await AddCardAsync(ws.Id);
        var batch1 = await CreateBatchAsync(U("batch1"), U("br1"));
        await Handler().Handle(new AddCardToBatchCommand(batch1.Name, card.Id), default);

        // batch2 is open; trying to steal the card should fail
        var batch2 = await CreateBatchAsync(U("batch2"), U("br2"));

        Func<Task> act = () => Handler().Handle(new AddCardToBatchCommand(batch2.Name, card.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not Closed*");
    }

    [Fact]
    public async Task CardInClosedBatch_AllowsReassignment()
    {
        // Arrange — card assigned to batch1 which is then Closed
        var ws = await CreateWorkspaceAsync();
        var card = await AddCardAsync(ws.Id);
        var batch1 = await CreateBatchAsync(U("batch1"), U("br1"));
        await Handler().Handle(new AddCardToBatchCommand(batch1.Name, card.Id), default);
        await TransitionToWorkingAsync(batch1.Id);
        await CloseAsync(batch1.Id, BatchClosedReason.Finished);

        // Reassigning to batch2 should succeed
        var batch2 = await CreateBatchAsync(U("batch2"), U("br2"));
        await Handler().Handle(new AddCardToBatchCommand(batch2.Name, card.Id), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.BatchId.Should().Be(batch2.Id);
    }
}

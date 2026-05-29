using Bishop.App.Batches.RemoveCardFromBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class RemoveCardFromBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public RemoveCardFromBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private RemoveCardFromBatchCommandHandler Handler() => new(_factory);

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId)
        => await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, SystemLaneNames.ToDo, U("card")), default);

    private async Task<Batch> CreateBatchAsync(string name, string branch)
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
            Status = BatchStatus.Open,
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

    private async Task AssignCardAsync(Guid batchId, Guid cardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.FindAsync(cardId);
        card!.BatchId = batchId;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand("no-such-batch", Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task MultipleBatchesSameName_Throws()
    {
        var name = U("batch");
        await CreateBatchAsync(name, U("br1"));
        await CreateBatchAsync(name, U("br2"));

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple*");
    }

    [Fact]
    public async Task BatchWorking_Throws()
    {
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await TransitionToWorkingAsync(batch.Id);

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await TransitionToWorkingAsync(batch.Id);
        await CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public async Task OpenBatch_UnassignsCard()
    {
        var ws = await CreateWorkspaceAsync();
        var card = await AddCardAsync(ws.Id);
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await AssignCardAsync(batch.Id, card.Id);

        await Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, card.Id), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.BatchId.Should().BeNull();
    }
}

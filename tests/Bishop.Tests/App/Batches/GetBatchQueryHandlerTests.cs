using Bishop.App.Batches.GetBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class GetBatchQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    private readonly Guid _wsId;

    public GetBatchQueryHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];
    private GetBatchQueryHandler Handler() => new(_factory);

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
        Func<Task> act = () => Handler().Handle(new GetBatchQuery("no-such-batch"), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task MultipleBatchesSameName_Throws()
    {
        var name = U("batch");
        await CreateBatchAsync(name, U("br1"));
        await CreateBatchAsync(name, U("br2"));

        Func<Task> act = () => Handler().Handle(new GetBatchQuery(name), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple*");
    }

    [Fact]
    public async Task NoCards_ReturnsBatchWithEmptyList()
    {
        var batch = await CreateBatchAsync(U("batch"), U("br"));

        var result = await Handler().Handle(new GetBatchQuery(batch.Name), default);

        result.Batch.Id.Should().Be(batch.Id);
        result.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task WithCards_ReturnsCardsOrderedByNumber()
    {
        var ws = await CreateWorkspaceAsync();
        var card1 = await AddCardAsync(ws.Id);
        var card2 = await AddCardAsync(ws.Id);
        var card3 = await AddCardAsync(ws.Id);
        var batch = await CreateBatchAsync(U("batch"), U("br"));
        await AssignCardAsync(batch.Id, card3.Id);
        await AssignCardAsync(batch.Id, card1.Id);
        await AssignCardAsync(batch.Id, card2.Id);

        var result = await Handler().Handle(new GetBatchQuery(batch.Name), default);

        result.Cards.Select(c => c.Id).Should().Equal(card1.Id, card2.Id, card3.Id);
    }

    [Fact]
    public async Task ExcludesCardsFromOtherBatch()
    {
        var ws = await CreateWorkspaceAsync();
        var card1 = await AddCardAsync(ws.Id);
        var card2 = await AddCardAsync(ws.Id);
        var batch = await CreateBatchAsync(U("batch"), U("br1"));
        var otherBatch = await CreateBatchAsync(U("other"), U("br2"));
        await AssignCardAsync(batch.Id, card1.Id);
        await AssignCardAsync(otherBatch.Id, card2.Id);

        var result = await Handler().Handle(new GetBatchQuery(batch.Name), default);

        result.Cards.Should().ContainSingle(c => c.Id == card1.Id);
        result.Cards.Should().NotContain(c => c.Id == card2.Id);
    }
}

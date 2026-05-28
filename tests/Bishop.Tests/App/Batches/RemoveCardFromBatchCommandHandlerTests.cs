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
    private BatchRepository Repo() => new(_factory);
    private RemoveCardFromBatchCommandHandler Handler() => new(Repo());

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId)
        => await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, SystemLaneNames.ToDo, U("card")), default);

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand("no-such-batch", Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task MultipleBatchesSameName_Throws()
    {
        var repo = Repo();
        var name = U("batch");
        await repo.CreateAsync(_wsId, name, U("br1"), "main", WorktreePath);
        await repo.CreateAsync(_wsId, name, U("br2"), "main", WorktreePath);

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple*");
    }

    [Fact]
    public async Task BatchWorking_Throws()
    {
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        Func<Task> act = () => Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public async Task OpenBatch_UnassignsCard()
    {
        var ws = await CreateWorkspaceAsync();
        var card = await AddCardAsync(ws.Id);
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.AssignCardAsync(batch.Id, card.Id);

        await Handler().Handle(new RemoveCardFromBatchCommand(batch.Name, card.Id), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.BatchId.Should().BeNull();
    }
}

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

    public GetBatchQueryHandlerTests(DbFixture fixture) => _factory = fixture.Factory;

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];
    private BatchRepository Repo() => new(_factory);
    private GetBatchQueryHandler Handler() => new(Repo(), _factory);

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
        Func<Task> act = () => Handler().Handle(new GetBatchQuery("no-such-batch"), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task MultipleBatchesSameName_Throws()
    {
        var repo = Repo();
        var name = U("batch");
        await repo.CreateAsync(name, U("br1"), "main", WorktreePath);
        await repo.CreateAsync(name, U("br2"), "main", WorktreePath);

        Func<Task> act = () => Handler().Handle(new GetBatchQuery(name), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple*");
    }

    [Fact]
    public async Task NoCards_ReturnsBatchWithEmptyList()
    {
        var repo = Repo();
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);

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
        var repo = Repo();
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);
        await repo.AssignCardAsync(batch.Id, card3.Id);
        await repo.AssignCardAsync(batch.Id, card1.Id);
        await repo.AssignCardAsync(batch.Id, card2.Id);

        var result = await Handler().Handle(new GetBatchQuery(batch.Name), default);

        result.Cards.Select(c => c.Id).Should().Equal(card1.Id, card2.Id, card3.Id);
    }

    [Fact]
    public async Task ExcludesCardsFromOtherBatch()
    {
        var ws = await CreateWorkspaceAsync();
        var card1 = await AddCardAsync(ws.Id);
        var card2 = await AddCardAsync(ws.Id);
        var repo = Repo();
        var batch = await repo.CreateAsync(U("batch"), U("br1"), "main", WorktreePath);
        var otherBatch = await repo.CreateAsync(U("other"), U("br2"), "main", WorktreePath);
        await repo.AssignCardAsync(batch.Id, card1.Id);
        await repo.AssignCardAsync(otherBatch.Id, card2.Id);

        var result = await Handler().Handle(new GetBatchQuery(batch.Name), default);

        result.Cards.Should().ContainSingle(c => c.Id == card1.Id);
        result.Cards.Should().NotContain(c => c.Id == card2.Id);
    }
}

using Bishop.App.Batches.AbandonBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class AbandonBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public AbandonBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId, string laneName = SystemLaneNames.ToDo)
        => await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, laneName, U("card")), default);

    private async Task<Batch> CreateWorkingBatchAsync(params Guid[] cardIds)
    {
        var repo = new BatchRepository(_factory);
        var slug = U("br");
        var batch = await repo.CreateAsync(_wsId, U("batch"), $"bishop/{slug}", "main", WorktreePath);
        foreach (var id in cardIds)
            await repo.AssignCardAsync(batch.Id, id);
        await repo.TransitionToWorkingAsync(batch.Id);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private ISender CreateSender()
    {
        var ghCli = Substitute.For<IGhCli>();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_factory, ghCli, NullLogger<MoveCardCommandHandler>.Instance)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private AbandonBatchCommandHandler CreateHandler(IGitCli? git = null, ISender? sender = null)
    {
        var gitSub = git ?? Substitute.For<IGitCli>();
        return new(new BatchRepository(_factory), gitSub, sender ?? CreateSender(), _factory);
    }

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new AbandonBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task BatchOpen_Throws()
    {
        var batch = await new BatchRepository(_factory).CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);

        Func<Task> act = () => CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchAlreadyClosed_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        Func<Task> act = () => CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_ClosesWithAbandoned_ReturnsZeroCards()
    {
        var batch = await CreateWorkingBatchAsync();

        var result = await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        result.CardsRestored.Should().Be(0);
        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Abandoned);
        saved.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CardsMovedToToDo()
    {
        var workspace = await CreateWorkspaceAsync();
        var c1 = await AddCardAsync(workspace.Id);
        var c2 = await AddCardAsync(workspace.Id);
        var batch = await CreateWorkingBatchAsync(c1.Id, c2.Id);

        await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        var savedC1 = await _db.Cards.SingleAsync(c => c.Id == c1.Id);
        var savedC2 = await _db.Cards.SingleAsync(c => c.Id == c2.Id);
        savedC1.LaneName.Should().Be(SystemLaneNames.ToDo);
        savedC2.LaneName.Should().Be(SystemLaneNames.ToDo);
    }

    [Fact]
    public async Task DoneCardsReopened()
    {
        var workspace = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id);
        var batch = await CreateWorkingBatchAsync(card.Id);

        var sender = CreateSender();
        await new MoveCardCommandHandler(_factory, Substitute.For<IGhCli>(), NullLogger<MoveCardCommandHandler>.Instance)
            .Handle(new MoveCardCommand(card.Id, SystemLaneNames.Done, 1, KeepOpen: false), default);

        await CreateHandler(sender: sender).Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LaneName.Should().Be(SystemLaneNames.ToDo);
        saved.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task CardsBatchIdCleared()
    {
        var workspace = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id);
        var batch = await CreateWorkingBatchAsync(card.Id);

        await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.BatchId.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsCardCount()
    {
        var workspace = await CreateWorkspaceAsync();
        var c1 = await AddCardAsync(workspace.Id);
        var c2 = await AddCardAsync(workspace.Id);
        var c3 = await AddCardAsync(workspace.Id);
        var batch = await CreateWorkingBatchAsync(c1.Id, c2.Id, c3.Id);

        var result = await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        result.CardsRestored.Should().Be(3);
    }

    [Fact]
    public async Task WorktreeRemovedWithWorkspacePath()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = Substitute.For<IGitCli>();

        await CreateHandler(git: git).Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).RemoveWorktreeAsync(WorkspacePath, WorktreePath, Arg.Any<CancellationToken>());
    }
}

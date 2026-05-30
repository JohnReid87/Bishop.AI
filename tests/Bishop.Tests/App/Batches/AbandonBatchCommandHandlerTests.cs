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

    private async Task<Batch> CreateBatchAsync(params Guid[] cardIds)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var slug = U("br");
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = U("batch"),
            BranchName = $"bishop/{slug}",
            BaseBranch = "main",
            WorktreePath = WorktreePath,
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        if (cardIds.Length > 0)
        {
            var cards = await db.Cards.Where(c => cardIds.Contains(c.Id)).ToListAsync();
            foreach (var card in cards)
                card.BatchId = batch.Id;
            await db.SaveChangesAsync();
        }

        batch.TransitionToWorking();
        await db.SaveChangesAsync();
        return batch;
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

    private AbandonBatchCommandHandler CreateHandler(IGitCli? git = null, ISender? sender = null) =>
        new(git ?? Substitute.For<IGitCli>(), sender ?? CreateSender(), _factory, TimeProvider.System);

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new AbandonBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task BatchOpen_ClosesWithAbandoned_ReturnsZeroCards()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        result.CardsRestored.Should().Be(0);
        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        _db.Entry(saved).Reload();
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Abandoned);
    }

    [Fact]
    public async Task BatchOpen_CardsBatchIdCleared()
    {
        var workspace = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id);

        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        var tracked = await db.Cards.SingleAsync(c => c.Id == card.Id);
        tracked.BatchId = batch.Id;
        await db.SaveChangesAsync();

        await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        _db.Entry(saved).Reload();
        saved.BatchId.Should().BeNull();
        saved.LaneName.Should().Be(SystemLaneNames.ToDo);
    }

    [Fact]
    public async Task BatchOpen_WorktreeRemoved()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        var git = Substitute.For<IGitCli>();
        await CreateHandler(git: git).Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).RemoveWorktreeAsync(WorkspacePath, WorktreePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BatchAlreadyClosed_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = WorktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        batch.Close(BatchClosedReason.Abandoned, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already Closed*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_ClosesWithAbandoned_ReturnsZeroCards()
    {
        var batch = await CreateBatchAsync();

        var result = await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        result.CardsRestored.Should().Be(0);
        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        _db.Entry(saved).Reload();
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
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

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
        var batch = await CreateBatchAsync(card.Id);

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
        var batch = await CreateBatchAsync(card.Id);

        await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        _db.Entry(saved).Reload();
        saved.BatchId.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsCardCount()
    {
        var workspace = await CreateWorkspaceAsync();
        var c1 = await AddCardAsync(workspace.Id);
        var c2 = await AddCardAsync(workspace.Id);
        var c3 = await AddCardAsync(workspace.Id);
        var batch = await CreateBatchAsync(c1.Id, c2.Id, c3.Id);

        var result = await CreateHandler().Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        result.CardsRestored.Should().Be(3);
    }

    [Fact]
    public async Task WorktreeRemovedWithWorkspacePath()
    {
        var batch = await CreateBatchAsync();
        var git = Substitute.For<IGitCli>();

        await CreateHandler(git: git).Handle(new AbandonBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).RemoveWorktreeAsync(WorkspacePath, WorktreePath, Arg.Any<CancellationToken>());
    }
}

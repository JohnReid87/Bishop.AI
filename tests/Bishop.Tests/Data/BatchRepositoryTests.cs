using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Data;

public sealed class BatchRepositoryTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;

    public BatchRepositoryTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private BatchRepository Repo() => new(_factory);

    private static string U(string prefix = "b") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Guid> CreateCardAsync()
    {
        var name = U("ws");
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(ws.Id, "To Do", "Test card"), default);
        return card.Id;
    }

    [Fact]
    public async Task Create_PersistsBatchWithOpenStatus()
    {
        // Arrange
        var repo = Repo();

        // Act
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");

        // Assert
        batch.Id.Should().NotBeEmpty();
        batch.Status.Should().Be(BatchStatus.Open);
        batch.ClosedReason.Should().BeNull();
        batch.ClosedAt.Should().BeNull();
        batch.CreatedAt.Should().NotBe(default);

        var stored = await _db.Batches.FindAsync(batch.Id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(BatchStatus.Open);
    }

    [Fact]
    public async Task GetByBranchName_ReturnsMatchingBatch()
    {
        // Arrange
        var repo = Repo();
        var branchName = U("br");
        await repo.CreateAsync(_wsId, U("name"), branchName, "main", @"C:\worktree");

        // Act
        var found = await repo.GetByBranchNameAsync(branchName);

        // Assert
        found.Should().NotBeNull();
        found!.BranchName.Should().Be(branchName);
    }

    [Fact]
    public async Task GetByBranchName_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var repo = Repo();

        // Act
        var found = await repo.GetByBranchNameAsync("nonexistent-branch-xyz");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task TransitionToWorking_ChangesStatusFromOpenToWorking()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");

        // Act
        var updated = await repo.TransitionToWorkingAsync(batch.Id);

        // Assert
        updated.Status.Should().Be(BatchStatus.Working);

        var stored = await _db.Batches.FindAsync(batch.Id);
        _db.Entry(stored!).Reload();
        stored!.Status.Should().Be(BatchStatus.Working);
    }

    [Fact]
    public async Task TransitionToWorking_Throws_WhenBatchIsAlreadyWorking()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        await repo.TransitionToWorkingAsync(batch.Id);

        // Act
        var act = () => repo.TransitionToWorkingAsync(batch.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Open*");
    }

    [Fact]
    public async Task Close_SetsClosed_WithFinishedReason()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");

        // Act
        var closed = await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        // Assert
        closed.Status.Should().Be(BatchStatus.Closed);
        closed.ClosedReason.Should().Be(BatchClosedReason.Finished);
        closed.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Close_SetsClosed_WithAbandonedReason()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        await repo.TransitionToWorkingAsync(batch.Id);

        // Act
        var closed = await repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Assert
        closed.Status.Should().Be(BatchStatus.Closed);
        closed.ClosedReason.Should().Be(BatchClosedReason.Abandoned);
        closed.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Close_Throws_WhenAlreadyClosed()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        // Act
        var act = () => repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already Closed*");
    }

    [Fact]
    public async Task AssignCard_SetsBatchIdOnCard()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var cardId = await CreateCardAsync();

        // Act
        await repo.AssignCardAsync(batch.Id, cardId);

        // Assert
        var card = await _db.Cards.FindAsync(cardId);
        _db.Entry(card!).Reload();
        card!.BatchId.Should().Be(batch.Id);
    }

    [Fact]
    public async Task AssignCard_Throws_WhenCardAlreadyInNonClosedBatch()
    {
        // Arrange
        var repo = Repo();
        var batch1 = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var batch2 = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var cardId = await CreateCardAsync();
        await repo.AssignCardAsync(batch1.Id, cardId);

        // Act — try to reassign to a different non-Closed batch
        var act = () => repo.AssignCardAsync(batch2.Id, cardId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not Closed*");
    }

    [Fact]
    public async Task AssignCard_AllowsReassignment_WhenPreviousBatchIsClosed()
    {
        // Arrange
        var repo = Repo();
        var batch1 = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var batch2 = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var cardId = await CreateCardAsync();
        await repo.AssignCardAsync(batch1.Id, cardId);
        await repo.CloseAsync(batch1.Id, BatchClosedReason.Finished);

        // Act
        await repo.AssignCardAsync(batch2.Id, cardId);

        // Assert
        var card = await _db.Cards.FindAsync(cardId);
        _db.Entry(card!).Reload();
        card!.BatchId.Should().Be(batch2.Id);
    }

    [Fact]
    public async Task AssignCard_Throws_WhenTargetBatchIsClosed()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var cardId = await CreateCardAsync();
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        // Act
        var act = () => repo.AssignCardAsync(batch.Id, cardId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Closed batch*");
    }

    [Fact]
    public async Task Delete_RemovesBatch_AndNullsBatchIdOnAssignedCards()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var cardId = await CreateCardAsync();
        await repo.AssignCardAsync(batch.Id, cardId);

        // Act
        await repo.DeleteAsync(batch.Id);

        // Assert
        var stored = await _db.Batches.FindAsync(batch.Id);
        stored.Should().BeNull();

        var card = await _db.Cards.FindAsync(cardId);
        _db.Entry(card!).Reload();
        card!.BatchId.Should().BeNull();
    }

    [Fact]
    public async Task SetFinishedAt_PersistsValue()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        var stamp = DateTimeOffset.UtcNow;

        // Act
        var updated = await repo.SetFinishedAtAsync(batch.Id, stamp);

        // Assert
        updated.FinishedAt.Should().Be(stamp);

        var stored = await _db.Batches.FindAsync(batch.Id);
        _db.Entry(stored!).Reload();
        stored!.FinishedAt.Should().Be(stamp);
    }

    [Fact]
    public async Task SetFinishedAt_ClearsValue_WhenNull()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("branch"), "main", @"C:\worktree");
        await repo.SetFinishedAtAsync(batch.Id, DateTimeOffset.UtcNow);

        // Act
        var updated = await repo.SetFinishedAtAsync(batch.Id, null);

        // Assert
        updated.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetFinishedAt_Throws_WhenBatchNotFound()
    {
        // Arrange
        var repo = Repo();

        // Act
        var act = () => repo.SetFinishedAtAsync(Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task List_ReturnsBatchesOrderedByCreatedAt()
    {
        // Arrange
        var repo = Repo();
        var branch1 = U("br");
        var branch2 = U("br");
        var b1 = await repo.CreateAsync(_wsId, U("name"), branch1, "main", @"C:\worktree");
        await Task.Delay(10); // ensure distinct CreatedAt
        var b2 = await repo.CreateAsync(_wsId, U("name"), branch2, "main", @"C:\worktree");

        // Act
        var all = await repo.ListAsync(_wsId);

        // Assert
        var ids = all.Select(b => b.Id).ToList();
        ids.Should().Contain(b1.Id);
        ids.Should().Contain(b2.Id);
        ids.IndexOf(b1.Id).Should().BeLessThan(ids.IndexOf(b2.Id));
    }
}

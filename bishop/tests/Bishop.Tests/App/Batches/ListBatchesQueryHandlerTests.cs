using Bishop.App.Batches.ListBatches;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class ListBatchesQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;

    public ListBatchesQueryHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private ListBatchesQueryHandler Handler(IGitCli? git = null)
    {
        if (git is null)
        {
            var stub = Substitute.For<IGitCli>();
            stub.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(false);
            stub.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(false);
            return new(_factory, stub);
        }
        return new(_factory, git);
    }

    private static string U(string prefix = "b") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateBatchAsync(
        string name,
        string branch,
        DateTimeOffset? createdAt = null,
        string worktreePath = @"C:\wt")
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = name,
            BranchName = branch,
            BaseBranch = "main",
            WorktreePath = worktreePath,
            Status = BatchStatus.Open,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private async Task<Card> CreateCardAsync(Guid? batchId, int number, string title = "Card")
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            BatchId = batchId,
            Title = title,
            LaneName = "To Do",
            Number = number,
            Position = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return card;
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
    public async Task ReturnsOpenAndWorkingBatches()
    {
        // Arrange
        var open = await CreateBatchAsync(U("name"), U("br"));
        var working = await CreateBatchAsync(U("name"), U("br"));
        await TransitionToWorkingAsync(working.Id);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().Contain(open.Id);
        results.Select(s => s.Batch.Id).Should().Contain(working.Id);
    }

    [Fact]
    public async Task ExcludesClosedBatch_ByDefault()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));
        await CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().NotContain(batch.Id);
    }

    [Fact]
    public async Task IncludesClosedBatch_WhenIncludeClosedTrue()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));
        await CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty, IncludeClosed: true), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().Contain(batch.Id);
    }

    [Fact]
    public async Task Cards_AreAttachedToBatchSummary()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));
        var card = await CreateCardAsync(batch.Id, number: 1, title: "Test card");

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.Cards.Should().HaveCount(1);
        summary.Cards[0].Id.Should().Be(card.Id);
        summary.CardCount.Should().Be(1);
    }

    [Fact]
    public async Task ReturnsBatchesSortedByCreatedAtAscending()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var newer = await CreateBatchAsync(U("name"), U("br"), createdAt: now);
        var older = await CreateBatchAsync(U("name"), U("br"), createdAt: now.AddMinutes(-10));
        var oldest = await CreateBatchAsync(U("name"), U("br"), createdAt: now.AddMinutes(-30));

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);
        var ordered = results
            .Where(r => r.Batch.Id == oldest.Id || r.Batch.Id == older.Id || r.Batch.Id == newer.Id)
            .ToList();

        // Assert
        ordered.Should().HaveCount(3);
        ordered[0].Batch.Id.Should().Be(oldest.Id);
        ordered[1].Batch.Id.Should().Be(older.Id);
        ordered[2].Batch.Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task CardsAreSortedByNumberAscending()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));
        await CreateCardAsync(batch.Id, number: 7);
        await CreateCardAsync(batch.Id, number: 2);
        await CreateCardAsync(batch.Id, number: 4);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.Cards.Select(c => c.Number).Should().ContainInOrder(2, 4, 7);
        summary.Cards[0].Number.Should().BeLessThan(summary.Cards[1].Number);
        summary.Cards[1].Number.Should().BeLessThan(summary.Cards[2].Number);
    }

    [Fact]
    public async Task NullBatchIdCard_IsExcluded()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));
        var attached = await CreateCardAsync(batch.Id, number: 1, title: "Attached");
        var orphan = await CreateCardAsync(batchId: null, number: 2, title: "Orphan");

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var allCards = results.SelectMany(s => s.Cards).ToList();
        allCards.Should().Contain(c => c.Id == attached.Id);
        allCards.Should().NotContain(c => c.Id == orphan.Id);
    }

    [Fact]
    public async Task WorktreeExists_FalseWhenPathEmpty()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"), worktreePath: string.Empty);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        results.Single(s => s.Batch.Id == batch.Id).WorktreeExists.Should().BeFalse();
    }

    [Fact]
    public async Task WorktreeExists_TrueWhenPathExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "bishop-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var batch = await CreateBatchAsync(U("name"), U("br"), worktreePath: tempDir);

            // Act
            var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

            // Assert
            results.Single(s => s.Batch.Id == batch.Id).WorktreeExists.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SurfacesGitState_MapsLocalBranchExistsToBranchExists()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));

        var git = Substitute.For<IGitCli>();
        git.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var results = await Handler(git).Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.BranchExists.Should().BeTrue();
        summary.IsMerged.Should().BeFalse();
    }

    [Fact]
    public async Task SurfacesGitState_MapsIsBranchMergedIntoToIsMerged()
    {
        // Arrange
        var batch = await CreateBatchAsync(U("name"), U("br"));

        var git = Substitute.For<IGitCli>();
        git.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var results = await Handler(git).Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.BranchExists.Should().BeFalse();
        summary.IsMerged.Should().BeTrue();
    }
}

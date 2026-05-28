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

    private BatchRepository Repo() => new(_factory);

    private ListBatchesQueryHandler Handler(IGitCli? git = null)
    {
        if (git is null)
        {
            var stub = Substitute.For<IGitCli>();
            stub.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(false);
            stub.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(false);
            return new(Repo(), _factory, stub);
        }
        return new(Repo(), _factory, git);
    }

    private static string U(string prefix = "b") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task ReturnsOpenAndWorkingBatches()
    {
        // Arrange
        var repo = Repo();
        var open = await repo.CreateAsync(_wsId, U("name"), U("br"), "main", @"C:\wt");
        var working = await repo.CreateAsync(_wsId, U("name"), U("br"), "main", @"C:\wt");
        await repo.TransitionToWorkingAsync(working.Id);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().Contain(open.Id);
        results.Select(s => s.Batch.Id).Should().Contain(working.Id);
    }

    [Fact]
    public async Task IncludesClosedBatch()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("br"), "main", @"C:\wt");
        await repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().Contain(batch.Id);
    }

    [Fact]
    public async Task Cards_AreAttachedToBatchSummary()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("br"), "main", @"C:\wt");

        await using var db = await _factory.CreateDbContextAsync();
        var workspace = await db.Workspaces.FindAsync(_wsId);
        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            BatchId = batch.Id,
            Title = "Test card",
            LaneName = "To Do",
            Number = 1,
            Position = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.Cards.Should().HaveCount(1);
        summary.Cards[0].Id.Should().Be(card.Id);
        summary.CardCount.Should().Be(1);
    }

    [Fact]
    public async Task SurfacesGitState()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(_wsId, U("name"), U("br"), "main", @"C:\wt");

        var git = Substitute.For<IGitCli>();
        git.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var results = await Handler(git).Handle(new ListBatchesQuery(_wsId, string.Empty), default);

        // Assert
        var summary = results.Single(s => s.Batch.Id == batch.Id);
        summary.BranchExists.Should().BeTrue();
        summary.IsMerged.Should().BeTrue();
        summary.WorktreeExists.Should().BeFalse();
    }
}

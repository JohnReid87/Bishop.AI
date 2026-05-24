using Bishop.App.Batches.ListBatches;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Batches;

public sealed class ListBatchesQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public ListBatchesQueryHandlerTests(DbFixture fixture) => _factory = fixture.Factory;

    private BatchRepository Repo() => new(_factory);
    private ListBatchesQueryHandler Handler() => new(Repo(), _factory);
    private static string U(string prefix = "b") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task ReturnsOpenAndWorkingBatches()
    {
        // Arrange
        var repo = Repo();
        var open = await repo.CreateAsync(U("name"), U("br"), "main", @"C:\wt");
        var working = await repo.CreateAsync(U("name"), U("br"), "main", @"C:\wt");
        await repo.TransitionToWorkingAsync(working.Id);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().Contain(open.Id);
        results.Select(s => s.Batch.Id).Should().Contain(working.Id);
    }

    [Fact]
    public async Task ExcludesClosedBatch_WhenNoPrUrl()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(U("name"), U("br"), "main", @"C:\wt");
        await repo.CloseAsync(batch.Id, BatchClosedReason.Abandoned);

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(), default);

        // Assert
        results.Select(s => s.Batch.Id).Should().NotContain(batch.Id);
    }

    [Fact]
    public async Task IncludesClosedBatch_WhenPrUrlSet()
    {
        // Arrange
        var repo = Repo();
        var batch = await repo.CreateAsync(U("name"), U("br"), "main", @"C:\wt");
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished, "https://github.com/owner/repo/pull/1");

        // Act
        var results = await Handler().Handle(new ListBatchesQuery(), default);

        // Assert
        var summary = results.Should().ContainSingle(s => s.Batch.Id == batch.Id).Subject;
        summary.Batch.GitHubPrUrl.Should().Be("https://github.com/owner/repo/pull/1");
    }
}

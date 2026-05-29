using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Data;

public sealed class BatchQueriesTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private readonly Guid _otherWsId;

    public BatchQueriesTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
        _otherWsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "b") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateBatchAsync(Guid workspaceId, string? name = null)
    {
        var repo = new BatchRepository(_factory);
        return await repo.CreateAsync(workspaceId, name ?? U("batch"), $"bishop/{U("br")}", "main", @"C:\wt");
    }

    // ── ByWorkspace ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ByWorkspace_ReturnsBatchesForWorkspace()
    {
        var b1 = await CreateBatchAsync(_wsId);
        var b2 = await CreateBatchAsync(_wsId);
        await CreateBatchAsync(_otherWsId);

        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByWorkspace(_wsId).ToListAsync();

        result.Select(b => b.Id).Should().Contain(b1.Id).And.Contain(b2.Id);
        result.Should().NotContain(b => b.WorkspaceId == _otherWsId);
    }

    [Fact]
    public async Task ByWorkspace_ReturnsEmpty_WhenNoBatchesForWorkspace()
    {
        var emptyWsId = Guid.NewGuid();

        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByWorkspace(emptyWsId).ToListAsync();

        result.Should().BeEmpty();
    }

    // ── ByName(name) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ByName_WithName_ReturnsMatchingBatches()
    {
        var name = U("named");
        var b1 = await CreateBatchAsync(_wsId, name);
        var b2 = await CreateBatchAsync(_otherWsId, name);
        await CreateBatchAsync(_wsId);

        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByName(name).ToListAsync();

        result.Select(b => b.Id).Should().Contain(b1.Id).And.Contain(b2.Id);
        result.Should().NotContain(b => b.Name != name);
    }

    [Fact]
    public async Task ByName_WithName_ReturnsEmpty_WhenNoMatch()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByName("no-such-batch").ToListAsync();

        result.Should().BeEmpty();
    }

    // ── ByName(workspaceId, name) ──────────────────────────────────────────────

    [Fact]
    public async Task ByName_WithWorkspaceAndName_ReturnsOnlyMatchingWorkspace()
    {
        var name = U("shared");
        var b1 = await CreateBatchAsync(_wsId, name);
        await CreateBatchAsync(_otherWsId, name);

        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByName(_wsId, name).ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(b1.Id);
    }

    [Fact]
    public async Task ByName_WithWorkspaceAndName_ReturnsEmpty_WhenNoMatch()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var result = await db.Batches.ByName(_wsId, "no-such-batch").ToListAsync();

        result.Should().BeEmpty();
    }
}

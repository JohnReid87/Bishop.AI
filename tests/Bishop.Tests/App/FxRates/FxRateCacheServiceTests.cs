using Bishop.App.FxRates;
using Bishop.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.FxRates;

public sealed class FxRateCacheServiceTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fixture;

    public FxRateCacheServiceTests(DbFixture fixture) => _fixture = fixture;

    private FxRateCacheService NewSut() => new(_fixture.Db);

    private async Task<Guid> SeedWorkspaceAsync()
    {
        var ws = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = $"ws-{Guid.NewGuid():N}",
            Path = $@"C:\tmp\{Guid.NewGuid():N}",
            Position = 1
        };
        _fixture.Db.Workspaces.Add(ws);
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();
        return ws.Id;
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNothingCached()
    {
        var workspaceId = await SeedWorkspaceAsync();

        var result = await NewSut().GetAsync(workspaceId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsCachedEntry_WhenExists()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var stamp = new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero);
        _fixture.Db.FxRates.Add(new FxRate { WorkspaceId = workspaceId, UsdToGbp = 0.75m, FetchedAtUtc = stamp });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var result = await NewSut().GetAsync(workspaceId);

        result.Should().NotBeNull();
        result!.UsdToGbp.Should().Be(0.75m);
        result.FetchedAtUtc.Should().Be(stamp);
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewRow_WhenNothingExists()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var stamp = new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero);

        await NewSut().UpsertAsync(workspaceId, 0.78m, stamp);

        var saved = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        saved.UsdToGbp.Should().Be(0.78m);
        saved.FetchedAtUtc.Should().Be(stamp);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRow_WhenRowExists()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var original = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);
        _fixture.Db.FxRates.Add(new FxRate { WorkspaceId = workspaceId, UsdToGbp = 0.70m, FetchedAtUtc = original });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var updated = new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero);
        await NewSut().UpsertAsync(workspaceId, 0.79m, updated);

        var saved = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        saved.UsdToGbp.Should().Be(0.79m);
        saved.FetchedAtUtc.Should().Be(updated);
    }
}

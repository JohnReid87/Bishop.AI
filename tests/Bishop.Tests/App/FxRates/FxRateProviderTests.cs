using Bishop.App.FxRates;
using Bishop.Core;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.FxRates;

public sealed class FxRateProviderTests
{
    private readonly IFxRateClient _client = Substitute.For<IFxRateClient>();
    private readonly IFxRateCache _cache = Substitute.For<IFxRateCache>();

    private FxRateProvider NewSut(Func<DateTimeOffset> now) => new(_client, _cache, now);

    private static DateTimeOffset At(int hour) =>
        new(2026, 5, 21, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsCachedRate_WhenCacheIsFreshToday()
    {
        var workspaceId = Guid.NewGuid();
        var cached = new FxRate { WorkspaceId = workspaceId, UsdToGbp = 0.77m, FetchedAtUtc = At(4) };
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(cached);
        var sut = NewSut(() => At(23));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.77m);
        await _client.DidNotReceive().FetchUsdToGbpAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToGbpAsync_FetchesAndCaches_WhenCacheIsStale()
    {
        var workspaceId = Guid.NewGuid();
        var stale = new FxRate { WorkspaceId = workspaceId, UsdToGbp = 0.70m, FetchedAtUtc = At(10).AddDays(-1) };
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(stale);
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns(0.78m);
        var now = At(10);
        var sut = NewSut(() => now);

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.78m);
        await _cache.Received(1).UpsertAsync(workspaceId, 0.78m, now.ToUniversalTime(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToGbpAsync_FetchesAndCaches_WhenNothingCached()
    {
        var workspaceId = Guid.NewGuid();
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns((FxRate?)null);
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns(0.78m);
        var now = At(10);
        var sut = NewSut(() => now);

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.78m);
        await _cache.Received(1).UpsertAsync(workspaceId, 0.78m, now.ToUniversalTime(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsStaleCacheRate_WhenFetchFails()
    {
        var workspaceId = Guid.NewGuid();
        var stale = new FxRate { WorkspaceId = workspaceId, UsdToGbp = 0.75m, FetchedAtUtc = At(10).AddDays(-1) };
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(stale);
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var sut = NewSut(() => At(10));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.75m);
        await _cache.DidNotReceive().UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsNull_WhenFetchFailsAndNothingCached()
    {
        var workspaceId = Guid.NewGuid();
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns((FxRate?)null);
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var sut = NewSut(() => At(10));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().BeNull();
    }

    [Fact]
    public async Task RefreshUsdToGbpAsync_FetchesAndCaches_IgnoringCache()
    {
        var workspaceId = Guid.NewGuid();
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns(0.79m);
        var now = At(23);
        var sut = NewSut(() => now);

        var rate = await sut.RefreshUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.79m);
        await _cache.Received(1).UpsertAsync(workspaceId, 0.79m, now.ToUniversalTime(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshUsdToGbpAsync_ReturnsNull_WhenFetchFails()
    {
        var workspaceId = Guid.NewGuid();
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var sut = NewSut(() => At(10));

        var rate = await sut.RefreshUsdToGbpAsync(workspaceId);

        rate.Should().BeNull();
        await _cache.DidNotReceive().UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToGbpAsync_FetchesAndCaches_WhenConstructedWithDefaultClock()
    {
        var workspaceId = Guid.NewGuid();
        _cache.GetAsync(workspaceId, Arg.Any<CancellationToken>()).Returns((FxRate?)null);
        _client.FetchUsdToGbpAsync(Arg.Any<CancellationToken>()).Returns(0.80m);
        var sut = new FxRateProvider(_client, _cache);

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.80m);
        await _cache.Received(1).UpsertAsync(
            workspaceId, 0.80m, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}

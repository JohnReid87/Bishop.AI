using System.Net;
using System.Text;
using Bishop.App.FxRates;
using Bishop.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.FxRates;

public sealed class FxRateProviderTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fixture;

    public FxRateProviderTests(DbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetUsdToGbpAsync_FetchesAndCaches_OnFirstCall()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var handler = new RecordingHandler(_ => OkJson("""{"rates":{"GBP":0.78}}"""));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.78m);
        handler.CallCount.Should().Be(1);
        var cached = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        cached.UsdToGbp.Should().Be(0.78m);
        cached.FetchedAtUtc.UtcDateTime.Date.Should().Be(new DateTime(2026, 5, 21));
    }

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsCachedRate_WhenFetchFails()
    {
        var workspaceId = await SeedWorkspaceAsync();
        _fixture.Db.FxRates.Add(new FxRate
        {
            WorkspaceId = workspaceId,
            UsdToGbp = 0.75m,
            FetchedAtUtc = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero)
        });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.75m);
        handler.CallCount.Should().Be(1);
        var cached = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        cached.UsdToGbp.Should().Be(0.75m);
        cached.FetchedAtUtc.UtcDateTime.Date.Should().Be(new DateTime(2026, 5, 20));
    }

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsNull_WhenFetchFailsAndNothingCached()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var handler = new RecordingHandler(_ => throw new HttpRequestException("network down"));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().BeNull();
        handler.CallCount.Should().Be(1);
        (await _fixture.Db.FxRates.AsNoTracking().AnyAsync(r => r.WorkspaceId == workspaceId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task GetUsdToGbpAsync_DoesNotCallHttp_WhenCacheFromToday()
    {
        var workspaceId = await SeedWorkspaceAsync();
        _fixture.Db.FxRates.Add(new FxRate
        {
            WorkspaceId = workspaceId,
            UsdToGbp = 0.77m,
            FetchedAtUtc = new DateTimeOffset(2026, 5, 21, 4, 0, 0, TimeSpan.Zero)
        });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var handler = new RecordingHandler(_ => throw new InvalidOperationException("should not be called"));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 23, 59, 0, TimeSpan.Zero));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.77m);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshUsdToGbpAsync_FetchesAndUpserts_EvenWhenCacheIsFresh()
    {
        var workspaceId = await SeedWorkspaceAsync();
        _fixture.Db.FxRates.Add(new FxRate
        {
            WorkspaceId = workspaceId,
            UsdToGbp = 0.70m,
            FetchedAtUtc = new DateTimeOffset(2026, 5, 21, 4, 0, 0, TimeSpan.Zero)
        });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var handler = new RecordingHandler(_ => OkJson("""{"rates":{"GBP":0.79}}"""));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 23, 59, 0, TimeSpan.Zero));

        var rate = await sut.RefreshUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.79m);
        handler.CallCount.Should().Be(1);
        var cached = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        cached.UsdToGbp.Should().Be(0.79m);
        cached.FetchedAtUtc.UtcDateTime.Should().Be(new DateTime(2026, 5, 21, 23, 59, 0));
    }

    [Fact]
    public async Task RefreshUsdToGbpAsync_ReturnsNullAndLeavesCacheUntouched_WhenFetchFails()
    {
        var workspaceId = await SeedWorkspaceAsync();
        _fixture.Db.FxRates.Add(new FxRate
        {
            WorkspaceId = workspaceId,
            UsdToGbp = 0.75m,
            FetchedAtUtc = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero)
        });
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();

        var handler = new RecordingHandler(_ => throw new HttpRequestException("network down"));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

        var rate = await sut.RefreshUsdToGbpAsync(workspaceId);

        rate.Should().BeNull();
        handler.CallCount.Should().Be(1);
        var cached = await _fixture.Db.FxRates.AsNoTracking().SingleAsync(r => r.WorkspaceId == workspaceId);
        cached.UsdToGbp.Should().Be(0.75m);
        cached.FetchedAtUtc.UtcDateTime.Date.Should().Be(new DateTime(2026, 5, 20));
    }

    [Fact]
    public async Task GetUsdToGbpAsync_ReturnsNull_WhenJsonResponseMissingRatesStructure()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var handler = new RecordingHandler(_ => OkJson("""{"message":"no data"}"""));
        var sut = NewSut(handler, () => new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().BeNull();
        handler.CallCount.Should().Be(1);
        (await _fixture.Db.FxRates.AsNoTracking().AnyAsync(r => r.WorkspaceId == workspaceId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task GetUsdToGbpAsync_FetchesRate_WhenCreatedWithTwoArgumentConstructor()
    {
        var workspaceId = await SeedWorkspaceAsync();
        var handler = new RecordingHandler(_ => OkJson("""{"rates":{"GBP":0.80}}"""));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.exchangerate.host/") };
        var sut = new FxRateProvider(client, _fixture.Db);

        var rate = await sut.GetUsdToGbpAsync(workspaceId);

        rate.Should().Be(0.80m);
    }

    private FxRateProvider NewSut(RecordingHandler handler, Func<DateTimeOffset> now)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.exchangerate.host/")
        };
        return new FxRateProvider(client, _fixture.Db, now);
    }

    private async Task<Guid> SeedWorkspaceAsync()
    {
        var ws = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = $"ws-{Guid.NewGuid():N}",
            Path = $"C:\\tmp\\{Guid.NewGuid():N}",
            Position = 1
        };
        _fixture.Db.Workspaces.Add(ws);
        await _fixture.Db.SaveChangesAsync();
        _fixture.Db.ChangeTracker.Clear();
        return ws.Id;
    }

    private static HttpResponseMessage OkJson(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }
}

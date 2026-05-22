using System.Net;
using System.Text;
using Bishop.App.FxRates;
using FluentAssertions;

namespace Bishop.Tests.App.FxRates;

public sealed class HttpFxRateClientTests
{
    private static HttpFxRateClient NewSut(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.exchangerate.host/") });

    private static HttpResponseMessage OkJson(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task FetchUsdToGbpAsync_ReturnsRate_WhenResponseContainsGbpRate()
    {
        var sut = NewSut(new FakeHandler(_ => OkJson("""{"rates":{"GBP":0.78}}""")));

        var rate = await sut.FetchUsdToGbpAsync();

        rate.Should().Be(0.78m);
    }

    [Fact]
    public async Task FetchUsdToGbpAsync_ReturnsNull_WhenResponseIsNotSuccess()
    {
        var sut = NewSut(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var rate = await sut.FetchUsdToGbpAsync();

        rate.Should().BeNull();
    }

    [Fact]
    public async Task FetchUsdToGbpAsync_ReturnsNull_WhenJsonMissingRatesStructure()
    {
        var sut = NewSut(new FakeHandler(_ => OkJson("""{"message":"no data"}""")));

        var rate = await sut.FetchUsdToGbpAsync();

        rate.Should().BeNull();
    }

    [Fact]
    public async Task FetchUsdToGbpAsync_ReturnsNull_WhenHttpRequestExceptionThrown()
    {
        var sut = NewSut(new FakeHandler(_ => throw new HttpRequestException("network down")));

        var rate = await sut.FetchUsdToGbpAsync();

        rate.Should().BeNull();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}

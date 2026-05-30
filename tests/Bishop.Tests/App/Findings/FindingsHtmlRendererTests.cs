using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bishop.App.Findings;
using FluentAssertions;

namespace Bishop.Tests.App.Findings;

public sealed class FindingsHtmlRendererTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);

    private static FindingsDocument MakeDocument(params Finding[] findings) =>
        new(findings);

    [Fact]
    public void Render_DeclaresSkillNameConstant_InHeadScript()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-dead-code",
            MakeDocument(new Finding("T", "B", "carded:#1")),
            RecordedAt,
            "sha1");

        html.Should().Contain("const SKILL_NAME=\"bish-dead-code\"");
        html.Should().Contain("window.chrome.webview.postMessage");
        html.Should().Contain("type:'convertToCard'");
    }

    [Fact]
    public void Render_EscapesSkillNameForJavaScript()
    {
        var html = FindingsHtmlRenderer.Render(
            "bad\"name",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("const SKILL_NAME=\"bad\\u0022name\"");
    }

    [Fact]
    public void Render_EmitsConvertButton_PerFinding()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(
                new Finding("First", "Body1", "carded:#1"),
                new Finding("Second", "Body2", "dismissed"),
                new Finding("Third", "Body3", "parked")),
            RecordedAt,
            "sha1");

        Regex.Matches(html, "<button class=\"convert-to-card\"")
            .Count.Should().Be(3);
    }

    [Fact]
    public void Render_EmitsButton_EvenForCardedFinding()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("Carded", "Body", "carded:#42")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<button class=\"convert-to-card\"");
    }

    [Fact]
    public void Render_ButtonPayload_RoundTripsThroughJson()
    {
        var finding = new Finding(
            Title: "Public type with no references",
            Body: "UnusedHelper is not called anywhere.",
            Outcome: "dismissed",
            Severity: "low",
            Location: "src/Foo.cs:42");

        var html = FindingsHtmlRenderer.Render(
            "bish-dead-code",
            MakeDocument(finding),
            RecordedAt,
            "sha1");

        var match = Regex.Match(html, "data-payload=\"(?<p>[^\"]*)\"");
        match.Success.Should().BeTrue();

        var decoded = WebUtility.HtmlDecode(match.Groups["p"].Value);
        using var doc = JsonDocument.Parse(decoded);
        var root = doc.RootElement;

        root.GetProperty("title").GetString().Should().Be("Public type with no references");
        root.GetProperty("body").GetString().Should().Be("UnusedHelper is not called anywhere.");
        root.GetProperty("severity").GetString().Should().Be("low");
        root.GetProperty("location").GetString().Should().Be("src/Foo.cs:42");
    }

    [Fact]
    public void Render_ButtonPayload_HandlesNullSeverityAndLocation()
    {
        var finding = new Finding("T", "B", "dismissed", Severity: null, Location: null);

        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(finding),
            RecordedAt,
            "sha1");

        var match = Regex.Match(html, "data-payload=\"(?<p>[^\"]*)\"");
        match.Success.Should().BeTrue();

        var decoded = WebUtility.HtmlDecode(match.Groups["p"].Value);
        using var doc = JsonDocument.Parse(decoded);
        var root = doc.RootElement;

        root.GetProperty("severity").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("location").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Render_ButtonPayload_EscapesQuotesInFindingFields()
    {
        var finding = new Finding(
            Title: "Title with \"quotes\" & <tag>",
            Body: "Body",
            Outcome: "dismissed");

        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(finding),
            RecordedAt,
            "sha1");

        var match = Regex.Match(html, "data-payload=\"(?<p>[^\"]*)\"");
        match.Success.Should().BeTrue();

        var decoded = WebUtility.HtmlDecode(match.Groups["p"].Value);
        using var doc = JsonDocument.Parse(decoded);
        doc.RootElement.GetProperty("title").GetString()
            .Should().Be("Title with \"quotes\" & <tag>");
    }
}

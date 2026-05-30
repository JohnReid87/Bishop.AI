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

    [Fact]
    public void Render_EmitsCompleteHtmlDocumentStructure()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<head>");
        html.Should().Contain("</head>");
        html.Should().Contain("<body>");
        html.Should().EndWith("</body></html>");
        html.Should().Contain("<meta charset=\"utf-8\">");
        html.Should().Contain("<title>bish-arch — findings</title>");
        html.Should().Contain("<header>");
        html.Should().Contain("</header>");
        html.Should().Contain("<h1>bish-arch</h1>");
        html.Should().Contain("<table id=\"findings\">");
        html.Should().Contain("<thead>");
        html.Should().Contain("</thead>");
        html.Should().Contain("<tbody>");
        html.Should().Contain("</tbody>");
        html.Should().Contain("</table>");
    }

    [Fact]
    public void Render_EmitsAllFiveColumnHeaders()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<th data-col=\"0\">Severity</th>");
        html.Should().Contain("<th data-col=\"1\">Title</th>");
        html.Should().Contain("<th data-col=\"2\">Location</th>");
        html.Should().Contain("<th data-col=\"3\">Outcome</th>");
        html.Should().Contain("<th data-col=\"4\">Action</th>");
    }

    [Fact]
    public void Render_UsesSingularFindingLabel_ForOneFinding()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("1 finding · recorded");
        html.Should().NotContain("1 findings");
    }

    [Fact]
    public void Render_UsesPluralFindingsLabel_ForZeroFindings()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(),
            RecordedAt,
            "sha1");

        html.Should().Contain("0 findings · recorded");
    }

    [Fact]
    public void Render_UsesPluralFindingsLabel_ForMultipleFindings()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(
                new Finding("T1", "B", "dismissed"),
                new Finding("T2", "B", "dismissed"),
                new Finding("T3", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("3 findings · recorded");
    }

    [Fact]
    public void Render_EmbedsCssAndScripts()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<style>");
        html.Should().Contain("</style>");
        html.Should().Contain(".sev-high");
        html.Should().Contain(".sev-med");
        html.Should().Contain(".sev-low");
        html.Should().Contain(".sev-other");
        html.Should().Contain(".oc-carded");
        html.Should().Contain(".oc-dismissed");
        html.Should().Contain(".oc-parked");
        html.Should().Contain("button.convert-to-card");
        html.Should().Contain("DOMContentLoaded");
        html.Should().Contain("document.querySelectorAll('button.convert-to-card')");
        html.Should().Contain("document.querySelectorAll('#findings thead th')");
    }

    [Theory]
    [InlineData("critical", "sev-high")]
    [InlineData("high", "sev-high")]
    [InlineData("medium", "sev-med")]
    [InlineData("med", "sev-med")]
    [InlineData("low", "sev-low")]
    [InlineData("info", "sev-low")]
    [InlineData("unknown", "sev-other")]
    [InlineData("CRITICAL", "sev-high")]
    public void Render_MapsSeverityToCssClass(string severity, string expectedClass)
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Severity: severity)),
            RecordedAt,
            "sha1");

        html.Should().Contain($"<span class=\"chip {expectedClass}\">{severity}</span>");
    }

    [Fact]
    public void Render_OmitsSeverityChip_WhenSeverityIsNull()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Severity: null)),
            RecordedAt,
            "sha1");

        var rowStart = html.IndexOf("<tbody>", StringComparison.Ordinal);
        var severityCellEnd = html.IndexOf("</td>", rowStart, StringComparison.Ordinal);
        var severityCell = html[rowStart..severityCellEnd];

        severityCell.Should().NotContain("<span class=\"chip");
        severityCell.Should().NotContain("sev-");
        html.Should().Contain("<tr><td></td>");
    }

    [Fact]
    public void Render_OmitsSeverityChip_WhenSeverityIsEmpty()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Severity: "")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<tr><td></td>");
        var rowStart = html.IndexOf("<tbody>", StringComparison.Ordinal);
        var rowEnd = html.IndexOf("</tbody>", rowStart, StringComparison.Ordinal);
        var rowHtml = html[rowStart..rowEnd];
        rowHtml.Should().NotContain("sev-other");
    }

    [Fact]
    public void Render_RendersLocationCell_WithLocClassAndEncodedValue()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Location: "src/Foo.cs:42")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<td class=\"loc\">src/Foo.cs:42</td>");
    }

    [Fact]
    public void Render_LocationCellIsEmpty_WhenLocationIsNull()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Location: null)),
            RecordedAt,
            "sha1");

        html.Should().Contain("<td class=\"loc\"></td>");
    }

    [Fact]
    public void Render_LocationCellIsEmpty_WhenLocationIsEmpty()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed", Location: "")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<td class=\"loc\"></td>");
    }

    [Fact]
    public void Render_RendersTitleInSummaryAndBodyInPre()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("MyTitle", "MyBody", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<details><summary>MyTitle</summary><pre>MyBody</pre></details>");
    }

    [Fact]
    public void Render_EscapesTitleAndBody()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("<b>T</b>", "a & b", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<summary>&lt;b&gt;T&lt;/b&gt;</summary>");
        html.Should().Contain("<pre>a &amp; b</pre>");
    }

    [Fact]
    public void Render_DismissedOutcome_RendersDismissedChip()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<span class=\"chip oc-dismissed\">dismissed</span>");
    }

    [Fact]
    public void Render_ParkedOutcome_RendersParkedChip()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "parked")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<span class=\"chip oc-parked\">parked</span>");
    }

    [Fact]
    public void Render_CardedOutcome_RendersCardedChipWithHashAndNumber()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "carded:#42")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<span class=\"chip oc-carded\">#42</span>");
    }

    [Fact]
    public void Render_UnknownOutcome_RendersGenericChipWithEncodedText()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "pending<>")),
            RecordedAt,
            "sha1");

        html.Should().Contain("<span class=\"chip\">pending&lt;&gt;</span>");
        var bodyStart = html.IndexOf("<tbody>", StringComparison.Ordinal);
        var bodyEnd = html.IndexOf("</tbody>", bodyStart, StringComparison.Ordinal);
        var body = html[bodyStart..bodyEnd];
        body.Should().NotContain("oc-dismissed");
        body.Should().NotContain("oc-parked");
        body.Should().NotContain("oc-carded");
    }

    [Fact]
    public void Render_OutcomeNotMatchingCardedPattern_DoesNotRenderAsCarded()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "carded:#abc")),
            RecordedAt,
            "sha1");

        var bodyStart = html.IndexOf("<tbody>", StringComparison.Ordinal);
        var bodyEnd = html.IndexOf("</tbody>", bodyStart, StringComparison.Ordinal);
        var body = html[bodyStart..bodyEnd];
        body.Should().NotContain("oc-carded");
        html.Should().Contain("<span class=\"chip\">carded:#abc</span>");
    }

    [Fact]
    public void Render_GitSha_TruncatesToSevenChars_WhenLonger()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "abcdef1234567890");

        html.Should().Contain(" · abcdef1");
        html.Should().NotContain("abcdef12");
    }

    [Fact]
    public void Render_GitSha_RendersInFull_WhenExactlySevenChars()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "abcdef1");

        html.Should().Contain(" · abcdef1");
    }

    [Fact]
    public void Render_GitSha_RendersInFull_WhenShorterThanSeven()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "abc");

        html.Should().Contain(" · abc");
    }

    [Fact]
    public void Render_OmitsGitShaSegment_WhenShaIsEmpty()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            RecordedAt,
            "");

        var metaStart = html.IndexOf("<div class=\"meta\">", StringComparison.Ordinal);
        var metaEnd = html.IndexOf("</div>", metaStart, StringComparison.Ordinal);
        var meta = html[metaStart..metaEnd];

        meta.Should().NotContain(" · abc");
        meta.Should().EndWith("UTC");
    }

    [Fact]
    public void Render_FormatsRecordedAtTimestamp_InExpectedFormat()
    {
        var html = FindingsHtmlRenderer.Render(
            "bish-arch",
            MakeDocument(new Finding("T", "B", "dismissed")),
            new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
            "sha1");

        html.Should().Contain("recorded 2026-05-30 12:00:00 UTC");
    }
}

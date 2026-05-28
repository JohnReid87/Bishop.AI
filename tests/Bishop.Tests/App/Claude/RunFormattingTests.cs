using Bishop.App.Services.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class RunFormattingTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.0k")]
    [InlineData(1234, "1.2k")]
    [InlineData(1500, "1.5k")]
    [InlineData(12345, "12.3k")]
    public void FormatTokens_KSuffix_AppliesAtOrAboveOneThousand(int input, string expected)
    {
        RunFormatting.FormatTokens(input).Should().Be(expected);
    }

    [Fact]
    public void FormatTokenSuffix_ReturnsNull_WhenBothCountsZero()
    {
        RunFormatting.FormatTokenSuffix(0, 0).Should().BeNull();
    }

    [Fact]
    public void FormatTokenSuffix_ShowsBothSides_WhenOneIsZero()
    {
        RunFormatting.FormatTokenSuffix(1234, 0).Should().Be("1.2k↑ 0↓");
        RunFormatting.FormatTokenSuffix(0, 500).Should().Be("0↑ 500↓");
    }

    [Fact]
    public void FormatTokenSuffix_RendersBothSidesWithKSuffix()
    {
        RunFormatting.FormatTokenSuffix(12345, 4123).Should().Be("12.3k↑ 4.1k↓");
    }

    [Fact]
    public void FormatTokenSuffix_BoundaryAt1000_FlipsToKSuffix()
    {
        RunFormatting.FormatTokenSuffix(999, 999).Should().Be("999↑ 999↓");
        RunFormatting.FormatTokenSuffix(1000, 1000).Should().Be("1.0k↑ 1.0k↓");
    }

    [Fact]
    public void FormatTokenSuffix_NegativeInput_DocumentsCurrentBehaviour()
    {
        RunFormatting.FormatTokenSuffix(-1, 200).Should().Be("-1↑ 200↓");
        RunFormatting.FormatTokenSuffix(100, -1).Should().Be("100↑ -1↓");
    }

    [Theory]
    [InlineData(0, "0ms")]
    [InlineData(500, "500ms")]
    [InlineData(999, "999ms")]
    public void FormatDuration_SubOneSecond_ReturnsMsString(long ms, string expected)
    {
        RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(ms)).Should().Be(expected);
    }

    [Fact]
    public void FormatDuration_Zero_ReturnsMsString()
    {
        RunFormatting.FormatDuration(TimeSpan.Zero).Should().Be("0ms");
    }

    [Theory]
    [InlineData(1000, "1.0s")]
    [InlineData(1500, "1.5s")]
    [InlineData(59500, "59.5s")]
    public void FormatDuration_SubOneMinute_ReturnsSecondsString(long ms, string expected)
    {
        RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(ms)).Should().Be(expected);
    }

    [Theory]
    [InlineData(60000, "1m0s")]
    [InlineData(90000, "1m30s")]
    [InlineData(3599000, "59m59s")]
    public void FormatDuration_SubOneHour_ReturnsMinutesSecondsString(long ms, string expected)
    {
        RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(ms)).Should().Be(expected);
    }

    [Theory]
    [InlineData(3600000, "1h0m")]
    [InlineData(5400000, "1h30m")]
    [InlineData(7200000, "2h0m")]
    public void FormatDuration_OneHourOrMore_ReturnsHoursMinutesString(long ms, string expected)
    {
        RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(ms)).Should().Be(expected);
    }

    [Fact]
    public void FormatDuration_NegativeDuration_DocumentsCurrentBehaviour()
    {
        RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(-1)).Should().Be("-1ms");
    }

    [Fact]
    public void FormatDuration_VeryLargeDuration_DocumentsCurrentBehaviour()
    {
        RunFormatting.FormatDuration(TimeSpan.FromHours(100)).Should().Be("100h0m");
    }

    [Fact]
    public void FormatTokens_NegativeInput_DocumentsCurrentBehaviour()
    {
        RunFormatting.FormatTokens(-1).Should().Be("-1");
    }

    [Fact]
    public void FormatTokens_VeryLargeValue_DocumentsCurrentBehaviour()
    {
        RunFormatting.FormatTokens(1_000_000).Should().Be("1000.0k");
    }

    [Fact]
    public void FormatFinalTokenSegment_ReturnsNull_WhenTotalsIsNull()
    {
        RunFormatting.FormatFinalTokenSegment(null).Should().BeNull();
    }

    [Fact]
    public void FormatFinalTokenSegment_ReturnsNull_WhenBothTokensZero()
    {
        RunFormatting.FormatFinalTokenSegment(new ClaudeRunTotals(0, 0)).Should().BeNull();
    }

    [Fact]
    public void FormatFinalTokenSegment_ReturnsNonNull_WhenOnlyInputIsZero()
    {
        RunFormatting.FormatFinalTokenSegment(new ClaudeRunTotals(0, 500))
            .Should().Be("0 in / 500 out");
    }

    [Fact]
    public void FormatFinalTokenSegment_ReturnsNonNull_WhenOnlyOutputIsZero()
    {
        RunFormatting.FormatFinalTokenSegment(new ClaudeRunTotals(500, 0))
            .Should().Be("500 in / 0 out");
    }

    [Fact]
    public void FormatFinalTokenSegment_FormatsInOut_WhenNoCache()
    {
        RunFormatting.FormatFinalTokenSegment(new ClaudeRunTotals(1234, 500))
            .Should().Be("1.2k in / 500 out");
    }

    [Fact]
    public void FormatFinalTokenSegment_AppendsCacheSuffix_WhenCacheReadTokensNonZero()
    {
        var totals = new ClaudeRunTotals(100, 200, CacheReadTokens: 300);

        RunFormatting.FormatFinalTokenSegment(totals)
            .Should().Be("100 in / 200 out · cache 300 read / 0 written");
    }

    [Fact]
    public void FormatFinalTokenSegment_AppendsCacheSuffix_WhenCacheCreationTokensNonZero()
    {
        var totals = new ClaudeRunTotals(100, 200, CacheCreationTokens: 400);

        RunFormatting.FormatFinalTokenSegment(totals)
            .Should().Be("100 in / 200 out · cache 0 read / 400 written");
    }

    [Fact]
    public void FormatFinalTokenSegment_AppendsCacheSuffix_WhenBothCacheValuesNonZero()
    {
        var totals = new ClaudeRunTotals(100, 200, CacheCreationTokens: 400, CacheReadTokens: 300);

        RunFormatting.FormatFinalTokenSegment(totals)
            .Should().Be("100 in / 200 out · cache 300 read / 400 written");
    }
}

using Bishop.App.Claude;
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
}

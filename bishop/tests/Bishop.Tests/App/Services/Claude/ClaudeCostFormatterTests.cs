using Bishop.App.Services.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Services.Claude;

public sealed class ClaudeCostFormatterTests
{
    [Theory]
    [InlineData(0.42, "$0.42")]
    [InlineData(3.1, "$3.10")]
    [InlineData(12.5, "$12.50")]
    [InlineData(0, "$0.00")]
    public void FormatUsd_UsesTwoDecimals_ForCentOrLargerAmounts(decimal cost, string expected)
    {
        ClaudeCostFormatter.FormatUsd(cost).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0143, "$0.0143")]
    [InlineData(0.4267, "$0.4267")]
    [InlineData(0.005, "$0.005")]
    public void FormatUsd_KeepsExtraPrecision_ForSmallAmounts(decimal cost, string expected)
    {
        ClaudeCostFormatter.FormatUsd(cost).Should().Be(expected);
    }

    [Fact]
    public void FormatCardFinding_ReturnsNull_WhenTotalsIsNull()
    {
        ClaudeCostFormatter.FormatCardFinding("claude-sonnet-4-6", null).Should().BeNull();
    }

    [Fact]
    public void FormatCardFinding_ReturnsNull_WhenCostIsZero()
    {
        var totals = new ClaudeRunTotals(1000, 200, 50, 100, 0m);
        ClaudeCostFormatter.FormatCardFinding("claude-sonnet-4-6", totals).Should().BeNull();
    }

    [Fact]
    public void FormatCardFinding_IncludesLabelledCost_ModelDisplayName_AndTokenBreakdown()
    {
        var totals = new ClaudeRunTotals(1200, 18000, 12000, 900000, 0.42m);

        var finding = ClaudeCostFormatter.FormatCardFinding("claude-sonnet-4-6", totals);

        finding.Should().NotBeNull();
        finding.Should().StartWith("**Auto-run cost (est.):** $0.42");
        finding.Should().Contain("Sonnet 4.6");
        finding.Should().Contain("reported by agent");
        finding.Should().Contain("cache");
    }

    [Fact]
    public void FormatCardFinding_FallsBackToRawModelId_WhenUnrecognised()
    {
        var totals = new ClaudeRunTotals(10, 5, 0, 0, 0.01m);

        var finding = ClaudeCostFormatter.FormatCardFinding("some-future-model", totals);

        finding.Should().Contain("some-future-model");
    }
}

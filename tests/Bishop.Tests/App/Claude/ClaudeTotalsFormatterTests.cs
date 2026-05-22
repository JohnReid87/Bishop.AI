using Bishop.App.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class ClaudeTotalsFormatterTests
{
    [Fact]
    public void Format_Returns_Null_When_All_Totals_Are_Zero()
    {
        ClaudeTotalsFormatter.Format(0, 0, 0).Should().BeNull();
    }

    [Fact]
    public void Format_Renders_Tokens_And_RunCount()
    {
        var line = ClaudeTotalsFormatter.Format(8100, 2400, 3);

        line.Should().Be("Claude: 3 runs, 8.1k in / 2.4k out");
    }

    [Fact]
    public void Format_Singular_Run_Uses_Run_Not_Runs()
    {
        var line = ClaudeTotalsFormatter.Format(500, 200, 1);

        line.Should().Be("Claude: 1 run, 500 in / 200 out");
    }

    [Fact]
    public void Format_Tokens_Below_1k_Render_As_Raw_Integer()
    {
        var line = ClaudeTotalsFormatter.Format(999, 1, 1);

        line.Should().Be("Claude: 1 run, 999 in / 1 out");
    }

    [Fact]
    public void Format_Tokens_At_Or_Above_1k_Render_With_K_Suffix_OneDecimal()
    {
        var line = ClaudeTotalsFormatter.Format(1000, 12345, 1);

        line.Should().Be("Claude: 1 run, 1.0k in / 12.3k out");
    }

    [Fact]
    public void Format_Returns_Non_Null_When_Only_RunCount_Is_NonZero()
    {
        var line = ClaudeTotalsFormatter.Format(0, 0, 1);

        line.Should().Be("Claude: 1 run, 0 in / 0 out");
    }

    [Fact]
    public void Format_Returns_Non_Null_When_Only_InputTokens_Is_NonZero()
    {
        var line = ClaudeTotalsFormatter.Format(500, 0, 0);

        line.Should().Be("Claude: 0 runs, 500 in / 0 out");
    }

    [Fact]
    public void Format_Returns_Non_Null_When_Only_OutputTokens_Is_NonZero()
    {
        var line = ClaudeTotalsFormatter.Format(0, 500, 0);

        line.Should().Be("Claude: 0 runs, 0 in / 500 out");
    }

    [Fact]
    public void Format_Negative_InputTokens_Renders_Negative_Token_Count()
    {
        var line = ClaudeTotalsFormatter.Format(-500, 200, 1);

        line.Should().Be("Claude: 1 run, -500 in / 200 out");
    }

    [Fact]
    public void Format_Negative_OutputTokens_Renders_Negative_Token_Count()
    {
        var line = ClaudeTotalsFormatter.Format(500, -200, 1);

        line.Should().Be("Claude: 1 run, 500 in / -200 out");
    }

    [Fact]
    public void Format_Negative_RunCount_Renders_Negative_Run_Count()
    {
        var line = ClaudeTotalsFormatter.Format(500, 200, -1);

        line.Should().Be("Claude: -1 runs, 500 in / 200 out");
    }

    [Fact]
    public void Format_VeryLargeTokenCounts_Render_With_K_Suffix()
    {
        var line = ClaudeTotalsFormatter.Format(5_000_000, 1_000_000, 10);

        line.Should().Be("Claude: 10 runs, 5000.0k in / 1000.0k out");
    }
}

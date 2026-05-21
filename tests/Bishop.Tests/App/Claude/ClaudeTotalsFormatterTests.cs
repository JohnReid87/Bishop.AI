using Bishop.App.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class ClaudeTotalsFormatterTests
{
    [Fact]
    public void Format_Returns_Null_When_All_Totals_Are_Zero()
    {
        ClaudeTotalsFormatter.Format(0m, 0, 0, 0, null).Should().BeNull();
    }

    [Fact]
    public void Format_Without_FxRate_Renders_UsdOnly()
    {
        var line = ClaudeTotalsFormatter.Format(0.12m, 8100, 2400, 3, null);

        line.Should().Be("Claude: $0.12 (3 runs, 8.1k in / 2.4k out)");
    }

    [Fact]
    public void Format_With_FxRate_Renders_GbpAlongsideUsd()
    {
        var line = ClaudeTotalsFormatter.Format(0.12m, 8100, 2400, 3, 0.75m);

        line.Should().Be("Claude: $0.12 (£0.09) (3 runs, 8.1k in / 2.4k out)");
    }

    [Fact]
    public void Format_Singular_Run_Uses_Run_Not_Runs()
    {
        var line = ClaudeTotalsFormatter.Format(0.05m, 500, 200, 1, null);

        line.Should().Be("Claude: $0.05 (1 run, 500 in / 200 out)");
    }

    [Fact]
    public void Format_Tokens_Below_1k_Render_As_Raw_Integer()
    {
        var line = ClaudeTotalsFormatter.Format(0.01m, 999, 1, 1, null);

        line.Should().Be("Claude: $0.01 (1 run, 999 in / 1 out)");
    }

    [Fact]
    public void Format_Tokens_At_Or_Above_1k_Render_With_K_Suffix_OneDecimal()
    {
        var line = ClaudeTotalsFormatter.Format(0.01m, 1000, 12345, 1, null);

        line.Should().Be("Claude: $0.01 (1 run, 1.0k in / 12.3k out)");
    }

    [Fact]
    public void Format_Renders_Even_When_Only_RunCount_Is_NonZero()
    {
        var line = ClaudeTotalsFormatter.Format(0m, 0, 0, 1, null);

        line.Should().Be("Claude: $0.00 (1 run, 0 in / 0 out)");
    }

    // ── Visible / hidden contract ─────────────────────────────────────────────
    // The CardDetailDialog binds row visibility to (Format result is not null).
    // These two cases pin down that contract.

    [Fact]
    public void Format_Hidden_State_When_All_Totals_Are_Zero_Returns_Null()
    {
        ClaudeTotalsFormatter.Format(0m, 0, 0, 0, null).Should().BeNull();
    }

    [Fact]
    public void Format_Visible_State_With_FxRate_Matches_Cli_ByteForByte()
    {
        var line = ClaudeTotalsFormatter.Format(0.12m, 8100, 2400, 3, 0.75m);

        line.Should().Be("Claude: $0.12 (£0.09) (3 runs, 8.1k in / 2.4k out)");
    }
}

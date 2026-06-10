using Bishop.Life.Core.Schema;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class GoalHorizonTests
{
    private static readonly DateOnly Today = new(2026, 6, 9);

    [Theory]
    [InlineData("2026-06")]   // current month → diff 0
    [InlineData("2026-07")]   // next month → diff 1
    [InlineData("2026-09")]   // diff 3 → inclusive boundary
    [InlineData("2026-05")]   // last month → diff -1, still within ≤3
    public void Bucket_WithinThreeMonths_IsMonth(string horizon)
        => GoalHorizon.Bucket(horizon, Today).Should().Be(GoalHorizon.Month);

    [Theory]
    [InlineData("2026-10")]   // diff 4, same year
    [InlineData("2026-12")]   // diff 6, same year
    public void Bucket_LaterThisYear_IsYear(string horizon)
        => GoalHorizon.Bucket(horizon, Today).Should().Be(GoalHorizon.Year);

    [Theory]
    [InlineData("2027-01")]   // next year
    [InlineData("2030-06")]   // far future
    public void Bucket_OutsideThisYear_IsBeyond(string horizon)
        => GoalHorizon.Bucket(horizon, Today).Should().Be(GoalHorizon.Beyond);

    [Fact]
    public void Bucket_PastMonth_IsMonth()
    {
        // Mirrors JS `bucket()` in index.html: `diff <= 3` includes negatives,
        // so overdue goals sit alongside imminent ones rather than vanishing
        // into Beyond.
        GoalHorizon.Bucket("2025-01", Today).Should().Be(GoalHorizon.Month);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("medium-term")]   // legacy enum value that triggered card #1037
    [InlineData("long-term")]
    [InlineData("2026-13")]       // invalid month
    [InlineData("2026-00")]
    [InlineData("not-a-date")]
    public void Bucket_InvalidOrMissing_IsBeyond(string? horizon)
        => GoalHorizon.Bucket(horizon, Today).Should().Be(GoalHorizon.Beyond);

    [Theory]
    [InlineData(null, true)]
    [InlineData("2026-12", true)]
    [InlineData("2026-01", true)]
    [InlineData("medium-term", false)]
    [InlineData("2026-13", false)]
    [InlineData("", false)]
    public void IsValid_AcceptsNullOrYearMonth(string? horizon, bool expected)
        => GoalHorizon.IsValid(horizon).Should().Be(expected);
}

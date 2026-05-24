using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class SkillRunRowViewModelTests
{
    [Fact]
    public void NeverRun_SetsRedStatusAndNeverText()
    {
        var row = new SkillRunRowViewModel("bish-arch", lastRun: null, commitsSince: null, shaUnreachable: false);

        row.LastRunText.Should().Be("Never");
        row.CommitsSinceText.Should().Be("—");
        row.StatusDotColor.Should().Be("#c97a8a");
        row.StatusTooltip.Should().Be("Never audited");
    }

    [Fact]
    public void ShaUnreachable_SetsReauditAndRedStatus()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-10), commitsSince: null, shaUnreachable: true);

        row.CommitsSinceText.Should().Be("Re-audit");
        row.StatusDotColor.Should().Be("#c97a8a");
        row.StatusTooltip.Should().Be("Audit SHA is no longer reachable from HEAD");
    }

    [Theory]
    [InlineData(0, "#4a9e6a")]
    [InlineData(9, "#4a9e6a")]
    [InlineData(10, "#c4a85f")]
    [InlineData(49, "#c4a85f")]
    [InlineData(50, "#c97a8a")]
    [InlineData(100, "#c97a8a")]
    public void CommitCountDeterminesStatusColor(int commitsSince, string expectedColor)
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-5), commitsSince, shaUnreachable: false);

        row.StatusDotColor.Should().Be(expectedColor);
    }

    [Fact]
    public void CommitsSinceText_IsCountAsString()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-1), 15, shaUnreachable: false);

        row.CommitsSinceText.Should().Be("15");
    }

    [Theory]
    [InlineData(0, "Fresh")]
    [InlineData(9, "Fresh")]
    [InlineData(10, "Getting stale")]
    [InlineData(49, "Getting stale")]
    [InlineData(50, "Stale — re-audit recommended")]
    public void StatusTooltip_ReflectsCommitThreshold(int commitsSince, string expectedTooltip)
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-1), commitsSince, shaUnreachable: false);

        row.StatusTooltip.Should().Be(expectedTooltip);
    }

    [Fact]
    public void SkillName_IsPreserved()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false);

        row.SkillName.Should().Be("bish-coverage");
    }

    [Fact]
    public void LastRunText_FormatsRelativeTime()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddHours(-3), 0, false);

        row.LastRunText.Should().Be("3h ago");
    }

    [Fact]
    public void NeverRun_SeverityRankIsRed()
    {
        var row = new SkillRunRowViewModel("bish-arch", null, null, false);

        row.SeverityRank.Should().Be(2);
    }

    [Fact]
    public void ShaUnreachable_SeverityRankIsRed()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-1), null, shaUnreachable: true);

        row.SeverityRank.Should().Be(2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(100, 2)]
    public void SeverityRank_MatchesCommitThresholds(int commitsSince, int expectedRank)
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddDays(-1), commitsSince, false);

        row.SeverityRank.Should().Be(expectedRank);
    }
}

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

    [Theory]
    [InlineData(-30,      "just now")] // 30 s  — < 60s boundary
    [InlineData(-3540,    "59m ago")]  // 59 min — < 60m boundary
    [InlineData(-3600,    "1h ago")]   // 60 min — >= 60m boundary
    [InlineData(-82800,   "23h ago")]  // 23 h   — < 24h boundary
    [InlineData(-86400,   "1d ago")]   // 24 h   — >= 24h boundary
    [InlineData(-2505600, "29d ago")]  // 29 d   — < 30d boundary
    [InlineData(-2592000, "1mo ago")]  // 30 d   — >= 30d boundary
    [InlineData(-7776000, "3mo ago")]  // 90 d   — multi-month
    public void LastRunText_FormatsRelativeTimeBoundaries(int offsetSeconds, string expected)
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow.AddSeconds(offsetSeconds), 0, false);

        row.LastRunText.Should().Be(expected);
    }

    [Fact]
    public void SelectModel_KnownId_UpdatesIdAndLabel()
    {
        var row = new SkillRunRowViewModel("bish-arch", null, null, false);

        row.SelectModelCommand.Execute("claude-opus-4-7");

        row.SelectedModelId.Should().Be("claude-opus-4-7");
        row.SelectedModelLabel.Should().Be("Opus 4.7 ▾");
    }

    [Fact]
    public void SelectModel_UnknownId_FallsBackToDefaultLabel()
    {
        var row = new SkillRunRowViewModel("bish-arch", null, null, false);

        row.SelectModelCommand.Execute("unknown-model");

        row.SelectedModelId.Should().Be("unknown-model");
        row.SelectedModelLabel.Should().Be("Sonnet 4.6 ▾");
    }

    [Fact]
    public void BishCoverage_WithWorkspacePath_ReturnsReportFilePath()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false, @"C:\myrepo");

        row.ReportFilePath.Should().Be(@"C:\myrepo\TestResults\coverage-report\index.html");
    }

    [Theory]
    [InlineData("bish-arch")]
    [InlineData("bish-tests")]
    [InlineData("bish-security")]
    [InlineData("bish-audit-docs")]
    public void NonCoverageSkill_NoFindingsFile_ReportFilePath_IsNull(string skillName)
    {
        var row = new SkillRunRowViewModel(skillName, null, null, false, @"C:\myrepo");

        row.ReportFilePath.Should().BeNull();
    }

    [Theory]
    [InlineData("bish-arch")]
    [InlineData("bish-tests")]
    [InlineData("bish-security")]
    [InlineData("bish-audit-docs")]
    public void NonCoverageSkill_WithFindingsFile_ReportFilePath_PointsAtIt(string skillName)
    {
        var workspace = Path.Combine(Path.GetTempPath(), "bishop-tests-" + Guid.NewGuid().ToString("N"));
        var findingsDir = Path.Combine(workspace, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);
        var findingsFile = Path.Combine(findingsDir, $"{skillName}.html");
        File.WriteAllText(findingsFile, "<html></html>");

        try
        {
            var row = new SkillRunRowViewModel(skillName, null, null, false, workspace);

            row.ReportFilePath.Should().Be(findingsFile);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void BishCoverage_WithoutWorkspacePath_ReportFilePath_IsNull()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false);

        row.ReportFilePath.Should().BeNull();
    }
}

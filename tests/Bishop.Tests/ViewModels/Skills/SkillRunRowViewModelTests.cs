using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Skills;

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
    public void NonCoverageSkill_ReportFilePath_IsAlwaysNull(string skillName)
    {
        var row = new SkillRunRowViewModel(skillName, null, null, false, @"C:\myrepo");

        row.ReportFilePath.Should().BeNull();
    }

    [Fact]
    public void BishCoverage_WithoutWorkspacePath_ReportFilePath_IsNull()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false);

        row.ReportFilePath.Should().BeNull();
    }

    [Fact]
    public void DisplayLabel_WithoutProjectName_IsSkillNameOnly()
    {
        var row = new SkillRunRowViewModel("bish-tests", null, null, false);

        row.ProjectName.Should().BeNull();
        row.DisplayLabel.Should().Be("bish-tests");
    }

    [Fact]
    public void DisplayLabel_WithProjectName_IncludesProject()
    {
        var row = new SkillRunRowViewModel("bish-tests", null, null, false, projectName: "Bishop.App.Tests");

        row.ProjectName.Should().Be("Bishop.App.Tests");
        row.DisplayLabel.Should().Be("bish-tests · Bishop.App.Tests");
    }

    [Fact]
    public void CanViewFindings_NonCoverageWithPositiveCount_IsTrue()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow, 0, false, findingsCount: 3);

        row.CanViewFindings.Should().BeTrue();
    }

    [Fact]
    public void CanViewFindings_BishCoverageWithPositiveCount_IsTrue()
    {
        var row = new SkillRunRowViewModel("bish-coverage", DateTimeOffset.UtcNow, 0, false, findingsCount: 3);

        row.CanViewFindings.Should().BeTrue();
    }

    [Fact]
    public void CanViewFindings_ZeroFindings_IsFalse()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow, 0, false, findingsCount: 0);

        row.CanViewFindings.Should().BeFalse();
    }

    [Fact]
    public void ViewFindings_RaisesEventWithNavArgs()
    {
        var wsId = Guid.NewGuid();
        var row = new SkillRunRowViewModel(
            "bish-tests", DateTimeOffset.UtcNow, 0, false,
            workspacePath: @"C:\repo",
            findingsCount: 2,
            projectName: "Bishop.App",
            workspaceId: wsId,
            gitHubRepo: "owner/repo");

        Bishop.ViewModels.Findings.FindingsPageNavArgs? captured = null;
        row.ViewFindingsRequested += a => captured = a;

        row.ViewFindingsCommand.Execute(null);

        captured.Should().NotBeNull();
        captured!.WorkspaceId.Should().Be(wsId);
        captured.WorkspacePath.Should().Be(@"C:\repo");
        captured.GitHubRepo.Should().Be("owner/repo");
        captured.SkillName.Should().Be("bish-tests");
        captured.ProjectName.Should().Be("Bishop.App");
    }

    [Fact]
    public void CanViewReport_BishCoverageWithWorkspacePath_IsTrue()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false, @"C:\myrepo");

        row.CanViewReport.Should().BeTrue();
    }

    [Fact]
    public void CanViewReport_NonCoverage_IsFalse()
    {
        var row = new SkillRunRowViewModel("bish-arch", null, null, false, @"C:\myrepo");

        row.CanViewReport.Should().BeFalse();
    }

    [Fact]
    public void CanViewReport_BishCoverageWithoutWorkspacePath_IsFalse()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false);

        row.CanViewReport.Should().BeFalse();
    }

    [Fact]
    public void ViewReport_RaisesEventWithUri()
    {
        var row = new SkillRunRowViewModel("bish-coverage", null, null, false, @"C:\myrepo");
        Uri? captured = null;
        row.ViewReportRequested += u => captured = u;

        row.ViewReportCommand.Execute(null);

        captured.Should().Be(new Uri(@"C:\myrepo\TestResults\coverage-report\index.html"));
    }

    [Fact]
    public void FindingsButtonText_ReflectsCount()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow, 0, false, findingsCount: 5);

        row.FindingsButtonText.Should().Be("View (5)");
    }

    [Fact]
    public void FindingsBadgeIsVisible_WhenFindingsCountNull_IsFalse()
    {
        var row = new SkillRunRowViewModel("bish-arch", null, null, false, findingsCount: null);

        row.FindingsCount.Should().BeNull();
        row.FindingsBadgeIsVisible.Should().BeFalse();
    }

    [Fact]
    public void FindingsBadgeIsVisible_WhenFindingsCountZero_IsTrue()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow, 0, false, findingsCount: 0);

        row.FindingsCount.Should().Be(0);
        row.FindingsBadgeIsVisible.Should().BeTrue();
    }

    [Fact]
    public void FindingsBadgeIsVisible_WhenFindingsCountPositive_IsTrue()
    {
        var row = new SkillRunRowViewModel("bish-arch", DateTimeOffset.UtcNow, 0, false, findingsCount: 5);

        row.FindingsCount.Should().Be(5);
        row.FindingsBadgeIsVisible.Should().BeTrue();
    }
}

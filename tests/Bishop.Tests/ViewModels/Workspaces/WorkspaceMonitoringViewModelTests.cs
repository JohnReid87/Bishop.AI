using Bishop.App.Git;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.Core;
using Bishop.Core.Skills;
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
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public class WorkspaceMonitoringViewModelTests
{
    private static readonly string[] TrackedSkills =
    [
        "bish-audit-docs",
        "bish-arch",
        "bish-tests",
        "bish-coverage",
        "bish-security",
    ];

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IGitCli _gitCli = Substitute.For<IGitCli>();
    private readonly WorkspaceMonitoringViewModel _vm;

    public WorkspaceMonitoringViewModelTests()
    {
        _vm = new WorkspaceMonitoringViewModel(_mediator, _gitCli, TimeProvider.System);
        _mediator
            .Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>([]));
        _mediator
            .Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InstalledSkill>>([]));
        _gitCli
            .GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));
    }

    [Fact]
    public async Task LoadAsync_PopulatesOneRowPerTrackedSkill()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().HaveCount(TrackedSkills.Length);
        _vm.Rows.Select(r => r.SkillName).Should().BeEquivalentTo(TrackedSkills);
    }

    [Fact]
    public async Task LoadAsync_SkillWithNoRun_ShowsNeverStatus()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.LastRunText.Should().Be("Never");
        row.StatusDotColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_SkillWithRecentRun_ShowsCommitCount()
    {
        var workspaceId = Guid.NewGuid();
        var sha = "abc123";
        _mediator
            .Send(Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    SkillName = "bish-arch",
                    RecordedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    GitSha = sha,
                }
            ]));
        _gitCli.GetCommitCountSinceAsync(sha, @"C:\fake", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(3));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.CommitsSinceText.Should().Be("3");
        row.StatusDotColor.Should().Be("#4a9e6a");
    }

    [Fact]
    public async Task LoadAsync_ShaUnreachable_ShowsReauditStatus()
    {
        var workspaceId = Guid.NewGuid();
        var sha = "deadbeef";
        _mediator
            .Send(Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    SkillName = "bish-security",
                    RecordedAt = DateTimeOffset.UtcNow.AddDays(-2),
                    GitSha = sha,
                }
            ]));
        _gitCli.GetCommitCountSinceAsync(sha, @"C:\fake", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(null));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-security");
        row.CommitsSinceText.Should().Be("Re-audit");
        row.StatusDotColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_PassesWorkspaceIdToQuery()
    {
        var workspaceId = Guid.NewGuid();

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        await _mediator.Received(1).Send(
            Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_AllClear_BadgeHidden()
    {
        _gitCli.GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));
        var workspaceId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
                TrackedSkills.Select(s => new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspaceId,
                    SkillName = s, RecordedAt = DateTimeOffset.UtcNow, GitSha = "abc"
                }).ToList()));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeCount.Should().Be(0);
        _vm.BadgeIsVisible.Should().BeFalse();
        _vm.BadgeColor.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_NeverRunSkills_BadgeIsRedWithCorrectCount()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c97a8a");
        _vm.BadgeCount.Should().Be(TrackedSkills.Length);
        _vm.BadgeTooltip.Should().Be($"{TrackedSkills.Length} of 5 reviews need attention");
    }

    [Fact]
    public async Task LoadAsync_OnlyAmberSkills_BadgeIsAmber()
    {
        var workspaceId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
                TrackedSkills.Select(s => new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspaceId,
                    SkillName = s, RecordedAt = DateTimeOffset.UtcNow, GitSha = "abc"
                }).ToList()));
        _gitCli.GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(15));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c4a85f");
        _vm.BadgeCount.Should().Be(TrackedSkills.Length);
    }

    [Fact]
    public async Task LoadAsync_MixOfRedAndAmber_BadgeIsRed()
    {
        var workspaceId = Guid.NewGuid();
        var archSha = "arch-sha";
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-arch", RecordedAt = DateTimeOffset.UtcNow, GitSha = archSha },
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", RecordedAt = DateTimeOffset.UtcNow, GitSha = "tests-sha" },
            ]));
        _gitCli.GetCommitCountSinceAsync(archSha, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(60));
        _gitCli.GetCommitCountSinceAsync(Arg.Is<string>(s => s != archSha), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(15));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_NoSkillMetadata_DefaultsToSonnet()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().AllSatisfy(r =>
        {
            r.SelectedModelId.Should().Be("claude-sonnet-4-6");
            r.SelectedModelLabel.Should().Be("Sonnet 4.6 ▾");
        });
    }

    [Fact]
    public async Task LoadAsync_NoPriorRun_UsesFirstRunModel()
    {
        _mediator
            .Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InstalledSkill>>(
            [
                new InstalledSkill("bish-arch", "", [], null, FirstRunModel: "claude-opus-4-7", ReRunModel: "claude-sonnet-4-6"),
            ]));

        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.SelectedModelId.Should().Be("claude-opus-4-7");
        row.SelectedModelLabel.Should().Be("Opus 4.7 ▾");
        row.ModelSelectionReason.Should().Be("(first run)");
    }

    [Fact]
    public async Task LoadAsync_WithPriorRun_UsesReRunModel()
    {
        var workspaceId = Guid.NewGuid();
        _mediator
            .Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InstalledSkill>>(
            [
                new InstalledSkill("bish-arch", "", [], null, FirstRunModel: "claude-opus-4-7", ReRunModel: "claude-sonnet-4-6"),
            ]));
        _mediator
            .Send(Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-arch", RecordedAt = DateTimeOffset.UtcNow.AddDays(-1), GitSha = "abc" },
            ]));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.SelectedModelId.Should().Be("claude-sonnet-4-6");
        row.ModelSelectionReason.Should().Be("(re-run default)");
    }

    [Fact]
    public async Task SelectedRow_WhenSet_ExposesReportUriForCoverageRow()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\myrepo");

        var coverageRow = _vm.Rows.First(r => r.SkillName == "bish-coverage");
        _vm.SelectedRow = coverageRow;

        _vm.SelectedReportUri.Should().Be(new Uri(@"C:\myrepo\TestResults\coverage-report\index.html"));
    }

    [Fact]
    public async Task SelectedRow_WhenSetToNonCoverageRow_SelectedReportUriIsNull()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\myrepo");

        _vm.SelectedRow = _vm.Rows.First(r => r.SkillName == "bish-arch");

        _vm.SelectedReportUri.Should().BeNull();
    }

    [Fact]
    public void SelectedRow_WhenNull_SelectedReportUriIsNull()
    {
        _vm.SelectedRow = null;

        _vm.SelectedReportUri.Should().BeNull();
    }

    [Fact]
    public async Task SelectedRow_WhenSetToCoverageRow_HasSelectedReportIsTrue()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\myrepo");

        _vm.SelectedRow = _vm.Rows.First(r => r.SkillName == "bish-coverage");

        _vm.HasSelectedReport.Should().BeTrue();
    }

    [Fact]
    public async Task SelectedRow_WhenSetToNonCoverageRow_HasSelectedReportIsFalse()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\myrepo");

        _vm.SelectedRow = _vm.Rows.First(r => r.SkillName == "bish-arch");

        _vm.HasSelectedReport.Should().BeFalse();
    }

    [Fact]
    public void HasSelectedReport_WhenSelectedRowIsNull_IsFalse()
    {
        _vm.SelectedRow = null;

        _vm.HasSelectedReport.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_ModelChangeDoesNotPersistAcrossRefresh()
    {
        _mediator
            .Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InstalledSkill>>(
            [
                new InstalledSkill("bish-arch", "", [], null, FirstRunModel: "claude-opus-4-7", ReRunModel: "claude-sonnet-4-6"),
            ]));

        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");
        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.SelectModelCommand.Execute("claude-haiku-4-5-20251001");

        await _vm.RefreshCommand.ExecuteAsync(null);

        var refreshedRow = _vm.Rows.First(r => r.SkillName == "bish-arch");
        refreshedRow.SelectedModelId.Should().Be("claude-opus-4-7");
    }
}
